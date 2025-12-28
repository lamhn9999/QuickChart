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
    private readonly IAutoChartService _autoChartService;
    private static char[] possibleOpen = new[] { '\"', '[' };
    private static char[] possibleEnd = new[] { '\"', ']' };
    public CSVService(ILQDbService lqdbService, IAutoChartService autoChartService)
    {
        _lqDbService = lqdbService;
        _autoChartService = autoChartService;
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
    
    private void DetectCategory(FieldValues fieldValues)
    {
        List <Category> result =  new List<Category>();
        List<string> valueStringlist = new List<string>();
        
        if(fieldValues.Field.Type != typeof(List<string>))
        {
            foreach (Value value in fieldValues.Values)
            {
                if (string.IsNullOrWhiteSpace(value.ValueString)) continue;
                valueStringlist.Add(value.ValueString.Trim());
            }   
        }

        double uniqPercentage = (1.0 * valueStringlist.Distinct().ToList().Count) / (fieldValues.Values.Count * 1.0);
        
        // Categorical
        if ((uniqPercentage <= Field.CATEGORICAL_THRESHOLD &&
            (fieldValues.Field.Type == typeof(string) || fieldValues.Field.Type == typeof(int)) ||
            fieldValues.Field.Type == typeof(bool)) 
            || fieldValues.Field.Type == typeof(List<string>)
            || fieldValues.Field.Type == typeof(DateTime)) result.Add(Category.Categorical);
        
        // Identifier
        if ((uniqPercentage >= Field.IDENTIFIER_THRESHOLD &&
             (fieldValues.Field.Type == typeof(string) || 
              fieldValues.Field.Type == typeof(int))) ||
            (fieldValues.Field.Type == typeof(string) && !result.Contains(Category.Categorical))) result.Add(Category.Identifier);
        
        // Numeric
        if ((fieldValues.Field.Type == typeof(int) || 
             fieldValues.Field.Type == typeof(double))) result.Add(Category.Numeric);

        fieldValues.Field.Categories = result;
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
                        value.ValueStringFormatted = null;
                        continue;
                    }
                    value.ValueStringFormatted = DateTime.ParseExact(value.ValueString, dateTimeFormat,
                        CultureInfo.InvariantCulture, DateTimeStyles.None).ToString();
                }
                return true;
            }
        }
        return false;
    }

    private bool DetectCategoryList(FieldValues fieldvalues)
    {
        HashSet<string> parsedSet = new();
        int parsedCount = 0;
        HashSet<string> nonparsedSet = new();
        int nonparsedCount = 0;
        
        foreach (Value v in fieldvalues.Values)
        {
            if (String.IsNullOrWhiteSpace(v.ValueString)) continue;
            string[] splitted = v.ValueString
                .Trim()
                .TrimStart(possibleOpen)
                .TrimEnd(possibleEnd)
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            foreach (string f in splitted)
            {
                parsedSet.Add(f.Trim());
                parsedCount++;
            }
        }

        foreach (Value v in fieldvalues.Values)
        {
            if (v.ValueString is null) continue;
            nonparsedSet.Add(v.ValueString.Trim());
            nonparsedCount++;
        }

        double rate1 = (1.0 * nonparsedSet.Count) / (1.0 * nonparsedCount);
        double rate2 = (1.0 * parsedSet.Count) / (1.0 * parsedCount);
        if (rate2 <= Field.CATEGORICAL_THRESHOLD && rate2 < rate1) 
        {
            // fieldvalues.Field.Categories.Add(Category.Categorical);
            foreach (var v in fieldvalues.Values)
            {
                if (string.IsNullOrWhiteSpace(v.ValueString))
                {
                    v.ValueStringFormatted = null;
                    continue;
                }

                v.ValueStringFormatted = string.Join(',',
                    v.ValueString
                        .Trim()
                        .TrimStart(possibleOpen)
                        .TrimEnd(possibleEnd)
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                );
                // Debug.WriteLine(v.ValueStringFormatted);
            }

            return true;
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
                    value.ValueStringFormatted = null;
                    continue;
                }
                value.ValueStringFormatted = (value.ValueString.ToLower().Trim() == "true" ||
                                              value.ValueString.ToLower().Trim() == "yes" ||
                                              value.ValueString.ToLower().Trim() == "1") ? "true" : "false";
            }
            return typeof(bool);
        }

        if (isInt)
        {
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString))
                {
                    value.ValueStringFormatted = null;
                    continue;
                }

                value.ValueStringFormatted = value.ValueString;
            }
            return typeof(int);
        }

        if (isDouble)
        {
            foreach (Value value in fieldvalues.Values)
            {
                if (String.IsNullOrWhiteSpace(value.ValueString))
                {
                    value.ValueStringFormatted = null;
                    continue;
                }

                value.ValueStringFormatted = value.ValueString;
            }
            return typeof(double);
        }

        if (DetectCategoryList(fieldvalues))
        {
            return typeof(List<string>);
        }
        
        foreach (Value value in fieldvalues.Values)
        {
            value.ValueStringFormatted = String.IsNullOrWhiteSpace(value.ValueString) ? null : value.ValueString;
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
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
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
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
        }
        _lqDbService.InsertTable(newTable);
        return true;  
    }

    public Table? GetTable(int analystid, string filename)
    {
        return _lqDbService.GetTable(analystid, filename);
    }

    public Table? GetTable(int analystId, int csvFileId)
    {
        return _lqDbService.GetTable(analystId, csvFileId);
    }
}