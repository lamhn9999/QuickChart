using System.Globalization;
using LQ.Data.Database;
using System.Diagnostics;
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
    
    private bool Validate(Analyst analyst, StreamReader reader)
    {
        int numCommas = -1;
        int numLines = 0;
        
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
            else if (cnt != numCommas)
            {
                Debug.WriteLine(line);
                return false;
            }
        }
        
        if (numCommas < 1 || numLines < 2) return false;
        return true;
    }

    public bool Validate(Analyst analyst, string path)
    {
        return Validate(analyst, new StreamReader(path)) && 
               !_lqDbService.SearchFileByAnalyst(analyst, Path.GetFileName(path));
    }

    public bool Validate(Analyst analyst, IFormFile file)
    {
        return Validate(analyst, new StreamReader(file.OpenReadStream())) && 
               !_lqDbService.SearchFileByAnalyst(analyst, file.FileName);
    }

    private bool DetectDateTime(FieldValues fieldvalues)
    {
        // string[] testformats = { "dd/MM/yyyy"};
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
        foreach (string dateTimeFormat in formats)
        {
            bool trueFormat = true;
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString)) continue;
                trueFormat &= DateTime.TryParseExact(value.ValueString.Trim(), dateTimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime dateRes);
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
        if (uniqueValue.All(x => boolTokens.Contains(x.Trim())) &&
            uniqueValue.Any(x => x.Trim() is "true" or "yes" or "1") &&
            uniqueValue.Any(x => x.Trim() is "false" or "no" or "0"))
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
    
    private Table? Interpret(CSVFile csvFile, StreamReader reader)
    {
        Table newTable = new Table(csvFile);

        List<string> fields = new List<string>();
        List<string> lines = new List<string>();
        
        string? headerLine = null;
        while ((headerLine = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(headerLine))
                break;
        }
    
        if (headerLine == null) return null;
    
        fields = headerLine.Split(',').Select(f => f.Trim()).ToList();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
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
            List<string> splits = new List<string>();
            int cntQuotes = 0;
            string temp = "";
            foreach (char c in lines[i])
            {
                if (c == ',')
                {
                    if (cntQuotes == 0)
                    {
                        splits.Add(temp);
                        temp = "";
                    }
                    else temp += c;
                }
                else
                {
                    temp += c;
                    if (c == '"') cntQuotes ^= 1;
                }
            }
            splits.Add(temp);
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
        if (!Validate(analyst, path)) 
            return false;
        CSVFile newCSVFile = new CSVFile(analyst, Path.GetFileName(path));
        Table? newTable = Interpret(newCSVFile, new StreamReader(path));
        if (newTable is null)
        {
            return false;
        }
        _lqDbService.InsertTable(newTable);
        return true;    
    }

    public bool ProcessUploadedCSV(Analyst analyst, IFormFile file)
    {
        if (!Validate(analyst, file)) 
            return false;
        CSVFile newCSVFile = new CSVFile(analyst, file.FileName);
        Table newTable = Interpret(newCSVFile, new StreamReader(file.OpenReadStream()));
        if (newTable is null)
        {
            return false;
        }
        _lqDbService.InsertTable(newTable);
        return true;  
    }

    public Table? GetTable(int analystid, string filename)
    {
        return _lqDbService.GetTable(analystid, filename);
    }
}