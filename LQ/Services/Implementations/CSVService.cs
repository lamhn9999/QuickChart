using System.Globalization;
using LQ.Data.Database;
using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Services.Interfaces;

namespace LQ.Services.Implementations;

public class CSVService : ICSVService
{
    private readonly ILQDbService _lqDbService;
    public CSVService(ILQDbService lqdbService)
    {
        _lqDbService = lqdbService;
    }
    
    public bool Validate(Analyst analyst, string path)
    {
        int numCommas = -1;
        int numLines = 0;
        using (var reader = new StreamReader(path))
        {
            string? line = null;
            while ((line = reader.ReadLine()) != null)
            {
                numLines++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                int cnt = 0, unit = 1;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == ',') cnt += unit; 
                    else if (line[i] == '"') unit = 1 - unit;
                }
                // System.Console.WriteLine(cnt);
                if (numCommas == -1) numCommas = cnt;
                else if (cnt != numCommas) return false;
            }
        }
        if (numCommas < 1 || numLines < 2) return false;
        return true;
    }

    private bool DetectDateTime(FieldValues fieldvalues)
    {
        string[] testformats = { "dd/MM/yyyy"};
        string[] formats = {"M/d/yyyy", "MM/dd/yyyy",                                    
            "d/M/yyyy", "dd/MM/yyyy", 
            "yyyy/M/d", "yyyy/MM/dd",
            "M-d-yyyy", "MM-dd-yyyy",                                    
            "d-M-yyyy", "dd-MM-yyyy", 
            "yyyy-M-d", "yyyy-MM-dd",
            "M.d.yyyy", "MM.dd.yyyy",                                    
            "d.M.yyyy", "dd.MM.yyyy", 
            "yyyy.M.d", "yyyy.MM.dd",
            "M,d,yyyy", "MM,dd,yyyy",                                    
            "d,M,yyyy", "dd,MM,yyyy", 
            "yyyy,M,d", "yyyy,MM,dd",
            "M d yyyy", "MM dd yyyy",                                    
            "d M yyyy", "dd MM yyyy", 
            "yyyy M d", "yyyy MM dd"
        };
        foreach (string dateTimeFormat in testformats)
        {
            bool trueFormat = true;
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString)) continue;
                trueFormat &= DateTime.TryParseExact(value.ValueString, dateTimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime dateRes);
                System.Console.WriteLine(dateRes);
            }

            if (trueFormat)
            {
                foreach (Value value in fieldvalues.Values)
                {
                    if (String.IsNullOrWhiteSpace(value.ValueString))
                    {
                        value.ValueCasted = null;
                        continue;
                    }
                    value.ValueCasted = DateTime.ParseExact(value.ValueString, dateTimeFormat,
                        CultureInfo.InvariantCulture, DateTimeStyles.None);
                }
                return true;
            }
        }
        return false;
    }
    private Type DetectType(FieldValues fieldvalues)
    {
        if (DetectDateTime(fieldvalues)) return typeof(DateTime);
        bool isInt = true;
        bool isDouble = true;
        List<String> valueStringList = new List<string>();
        foreach (Value value in fieldvalues.Values)
        {
            if (String.IsNullOrWhiteSpace(value.ValueString)) continue;
            if (!int.TryParse(value.ValueString, out int result)) isInt = false;
            if (!double.TryParse(value.ValueString, out double resultd)) isDouble = false;
            valueStringList.Add(value.ValueString.ToLower());
        }

        List<String> uniqueValue = valueStringList.Distinct().ToList();
        var boolTokens = new HashSet<string> { "true", "false", "yes", "no", "0", "1" };
        if (uniqueValue.All(x => boolTokens.Contains(x)) &&
            uniqueValue.Any(x => x is "true" or "yes" or "1") &&
            uniqueValue.Any(x => x is "false" or "no" or "0"))
        {
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString))
                {
                    value.ValueCasted = null;
                    continue;
                }
                value.ValueCasted = (value.ValueString == "true" ||
                                     value.ValueString == "yes" ||
                                     value.ValueString == "1");
            }
            return typeof(bool);
        }

        if (isInt)
        {
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString))
                {
                    value.ValueCasted = null;
                    continue;
                }

                value.ValueCasted = int.Parse(value.ValueString);
            }
            return typeof(int);
        }

        if (isDouble)
        {
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString))
                {
                    value.ValueCasted = null;
                    continue;
                }

                value.ValueCasted = float.Parse(value.ValueString);
            }
            return typeof(double);
        }
        
        foreach (Value value in fieldvalues.Values)
        {
            if (String.IsNullOrWhiteSpace(value.ValueString))
            {
                value.ValueCasted = null;
                continue;
            }

            value.ValueCasted = value.ValueString;
        }
        return typeof(string);
    }

    private Table Interpret(CSVFile csvFile, string path)
    {
        Table newTable = new Table(csvFile);

        List<string> fields = new List<string>();
        List<string> lines = new List<string>();
        using (var reader = new StreamReader(path))
        {
            string? headerLine = null;
            while ((headerLine = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(headerLine))
                    break;
            }
        
            if (headerLine == null)
            {
                return newTable;
            }
        
            fields = headerLine.Split(',').Select(f => f.Trim()).ToList();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }
        
        for (int i = 0; i < fields.Count; i++)
        {
            string trimmedfield = fields[i].Trim();
            // if (trimmedfield == "") 
            Field newField = new Field()
                { Name = trimmedfield, CSVFile = csvFile, Index = i, Categories = new List<Category>() };
            newTable.Columns.Add(new FieldValues{Field = newField, Values = new List<Value>()});
        }
        
        for (int i = 0; i < lines.Count; i++)
        {
            List<string> splits = lines[i].Split(',').ToList();
            List<string?> newValues = new List<string?>();
            foreach (string split in splits)
            {
                if(string.IsNullOrEmpty(split)) newValues.Add(null);
                else newValues.Add(split);
            }
            newTable.AddRow(i, newValues);
        }

        foreach (FieldValues fieldvalue in newTable.Columns)
        {
            fieldvalue.Field.Type = DetectType(fieldvalue);
        }
        return newTable;
    }

    public bool ProcessUploadedCSV(Analyst analyst, string path)
    {
        // System.Console.WriteLine(Validate(analyst, path));
        if (!Validate(analyst, path) || _lqDbService.SearchFileByAnalyst(analyst, Path.GetFileName(path))) 
            return false;
        CSVFile newCSVFile = new CSVFile(analyst, Path.GetFileName(path));
        Table newTable = Interpret(newCSVFile, path);
        _lqDbService.InsertTable(newTable);
        newTable.Show(25);
        return true;    
    }

    public Table? GetTable(int analystid, string filename)
    {
        return _lqDbService.GetTable(analystid, filename);
    }
}