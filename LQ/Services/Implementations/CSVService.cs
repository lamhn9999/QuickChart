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

    public bool DetectDateTime(FieldValues fieldvalues)
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
                if (dateTimeFormat == "MM/dd/yyyy" && fieldvalues.Field.Name == "Date" && !DateTime.TryParseExact(value.ValueString.Trim(), dateTimeFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime dateRes2))
                {
                    Debug.WriteLine(value.ValueString.Trim());
                }
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
        HashSet<string> nonparsedSet = new HashSet<string>();
        int nonparsedCount = 0;
        HashSet<char> charCandidates = new HashSet<char>();

        foreach (Value v in fieldvalues.Values)
        {
            if (string.IsNullOrWhiteSpace(v.ValueString)) continue;
            string val = v.ValueString.Trim().TrimStart(possibleOpen).TrimEnd(possibleEnd);
            nonparsedSet.Add(val);
            nonparsedCount++;

            foreach (char c in val)
            {
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                {
                    charCandidates.Add(c);
                }
            }
        }

        if (nonparsedCount == 0 || nonparsedSet.Count == 0) return false;

        double rate1 = (double)nonparsedSet.Count / nonparsedCount;

        char bestSeparator = '\0';
        double bestRate2 = double.MaxValue;
        int bestParsedCount = 0;

        // Ensure comma is checked
        if (!charCandidates.Contains(',')) charCandidates.Add(',');

        foreach (char sep in charCandidates)
        {
            HashSet<string> parsedSet = new HashSet<string>();
            int parsedCount = 0;

            foreach (Value v in fieldvalues.Values)
            {
                if (string.IsNullOrWhiteSpace(v.ValueString)) continue;
                string s = v.ValueString.Trim().TrimStart(possibleOpen).TrimEnd(possibleEnd);
                
                string[] splitted = s.Split(sep)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();

                foreach (string f in splitted)
                {
                    parsedSet.Add(f);
                    parsedCount++;
                }
            }

            // Must actively split items to be considered a list separator
            if (parsedCount > nonparsedCount)
            {
                double rate2 = (double)parsedSet.Count / parsedCount;
                if (rate2 <= Field.CATEGORICAL_THRESHOLD && rate2 < rate1)
                {
                    // If rates tie, prefer the separator that results in more items
                    if (rate2 < bestRate2 || (rate2 == bestRate2 && parsedCount > bestParsedCount))
                    {
                        bestRate2 = rate2;
                        bestSeparator = sep;
                        bestParsedCount = parsedCount;
                    }
                }
            }
        }

        if (bestSeparator != '\0') 
        {
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
                        .Split(bestSeparator)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                );
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

    public async Task<bool> ProcessUploadedCSV(Analyst analyst, string path)
    {
        if (!Validate(analyst, path)) 
            return false;
        CSVFile newCSVFile = new CSVFile(analyst.Id, Path.GetFileName(path));
        Table? newTable = Interpret(newCSVFile, new StreamReader(path));
        if (newTable is null)
        {
            return false;
        }
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
        }
        
        await _lqDbService.InsertTableAsync(newTable);
        return true;    
    }

    public async Task<bool> ProcessUploadedCSV(Analyst analyst, IFormFile file)
    {
        Console.WriteLine(file is null);
        if (!Validate(analyst, file)) 
            return false;
        CSVFile newCSVFile = new CSVFile(analyst.Id, file.FileName);
        Table newTable = Interpret(newCSVFile, new StreamReader(file.OpenReadStream()));
        if (newTable is null)
        {
            return false;
        }
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
        }

        await _lqDbService.InsertTableAsync(newTable);
        return true;  
    }

    public Table? GetTable(string analystid, string filename)
    {
        return _lqDbService.GetTable(analystid, filename);
    }

    public Table? GetTable(string analystId, int csvFileId)
    {
        return _lqDbService.GetTable(analystId, csvFileId);
    }

    public Table? GetTableLimited(int csvFileId, int numRowLimited)
    {
        return _lqDbService.GetTableLimited(csvFileId, numRowLimited);
    }

    public void RemoveCSVFile(string analystId, string fileName)
    {
        int csvFileId = _lqDbService.GetCSVFile(analystId, fileName).ID;
        _lqDbService.RemoveCSVFile(csvFileId);
    }

    public void RenameCSVFile(string analystId, string fileName, string newFileName)
    {
        Console.WriteLine(analystId + " - " + fileName + " - " + newFileName);
        Console.WriteLine(_lqDbService.GetCSVFile(analystId, fileName));
        int csvFileId = _lqDbService.GetCSVFile(analystId, fileName).ID;
        _lqDbService.RenameCSVFile(csvFileId, newFileName);
    }

    public CSVFile? GetCSVFile(string analystid, string filename)
    {
        return _lqDbService.GetCSVFile(analystid, filename);
    }
}