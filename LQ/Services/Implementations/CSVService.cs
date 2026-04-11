using System.Globalization;
using System.Diagnostics;
using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Services.Interfaces;

namespace LQ.Services.Implementations;

static class TypeNameMap
{
    public static Dictionary<string, Type> Map = new Dictionary<string, Type>
    {
        { "string",  typeof(string) },
        { "int",     typeof(int) },
        { "double",  typeof(double) },
        { "datetime", typeof(DateTime) },
        { "bool",    typeof(bool) },
        { "list", typeof(List<string>) }
    };
}

public class CSVService : TableRepository, ICSVService
{
    public static readonly char[] possibleOpen = new[] { '\"', '[' };
    public static readonly char[] possibleEnd = new[] { '\"', ']' };
    
    public CSVService(LQDbContext lqDbContext) : base(lqDbContext)
    {
    }

    private static bool Validate(Analyst analyst, StreamReader reader)
    {
        var numCommas = -1;
        var numLines = 0;

        while (reader.ReadLine() is { } line)
        {
            numLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            int cnt = 0, unit = 1;
            foreach (var t in line)
            {
                switch (t)
                {
                    case ',':
                        cnt += unit;
                        break;
                    case '"':
                        unit = 1 - unit;
                        break;
                }
            }
            if (numCommas == -1) numCommas = cnt;
            else if (cnt != numCommas)
            {
                Debug.WriteLine(line);
                return false;
            }
        }
        
        return numCommas >= 1 && numLines >= 2;
    }

    public async Task<bool> Validate(Analyst analyst, string path)
    {
        return !await FileExists(analyst, Path.GetFileName(path)) && Validate(analyst, new StreamReader(path));
    }

    public async Task<bool> Validate(Analyst analyst, IFormFile file)
    {
        return !await FileExists(analyst, file.FileName) && Validate(analyst, new StreamReader(file.OpenReadStream()));
    }
    
    private static void DetectCategory(FieldValues fieldValues)
    {
        List <Category> result = [];
        List <string> valueStringlist = [];
        
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

    private static bool DetectDateTime(FieldValues fieldvalues)
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

        double rateNonParsed = (double)nonparsedSet.Count / nonparsedCount;

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
                double rateParsed = (double)parsedSet.Count / parsedCount;
                if (rateParsed <= Field.CATEGORICAL_THRESHOLD && rateParsed < rateNonParsed)
                {
                    // If rates tie, prefer the separator that results in more items
                    if (rateParsed < bestRate2 || (rateParsed == bestRate2 && parsedCount > bestParsedCount))
                    {
                        bestRate2 = rateParsed;
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

    private bool DetectIdentifierList(FieldValues fieldvalues)
    {
        Console.WriteLine($"DetectIdentifierList {fieldvalues.Field.Name}");
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

        double rateNonParsed = (double)nonparsedSet.Count / nonparsedCount;

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
                double rateParsed = (double)parsedSet.Count / parsedCount;
                if (rateParsed > Field.CATEGORICAL_THRESHOLD && rateParsed < rateNonParsed && rateParsed <= Field.IDENTIFIER_LIST_THRESHOLD)
                {
                    // If rates tie, prefer the separator that results in more items
                    if (rateParsed < bestRate2 || (rateParsed == bestRate2 && parsedCount > bestParsedCount))
                    {
                        bestRate2 = rateParsed;
                        bestSeparator = sep;
                        bestParsedCount = parsedCount;
                    }
                }
            }
        }
        
        Console.WriteLine($"bestRate2: {bestRate2}");
        Console.WriteLine($"rateNonParsed: {rateNonParsed}");

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

        if (DetectCategoryList(fieldvalues) || DetectIdentifierList(fieldvalues))
        {
            return typeof(List<string>);
        }
        
        foreach (Value value in fieldvalues.Values)
        {
            value.ValueStringFormatted = String.IsNullOrWhiteSpace(value.ValueString) ? null : value.ValueString;
        }
        return typeof(string);
    }

    private List<string?> SplitField(string line)
    {
        List<string?> splits = new();
        int cntQuotes = 0;
        string temp = "";
        foreach (char c in line)
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
        return splits;
    }
    
    private Table? Interpret(CSVFile csvFile, StreamReader reader)
    {
        Table newTable = new Table(csvFile);
        List<string> lines = new List<string>();
        
        string? headerLine = null;
        while ((headerLine = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(headerLine))
                break;
        }
    
        if (headerLine == null) return null;
    
        var fields = SplitField(headerLine);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }
        
        for (int i = 0; i < fields.Count; i++)
        {
            string trimmedfield = fields[i].Trim();
            Field newField = new Field()
                { Name = trimmedfield, CSVFile = csvFile, Index = i, Categories = new List<Category>() };
            newTable.Columns.Add(new FieldValues{Field = newField, Values = new List<Value>()});
        }
        
        for (int i = 0; i < lines.Count; i++)
        {
            List<string?> splits = SplitField(lines[i]);
            List<string?> newValues = new List<string?>();
            foreach (string? split in splits)
            {
                newValues.Add(split);
            }
            newTable.AddRow(i, newValues);
        }

        foreach (FieldValues fieldvalue in newTable.Columns)
        {
            fieldvalue.Field.Type = DetectType(fieldvalue);
        }
        return newTable;
    }

    public async Task<bool> ProcessUploaded(Analyst analyst, string path)
    {
        if (!await Validate(analyst, path)) 
            return false;
        CSVFile newFile = new CSVFile(analyst.Id, Path.GetFileName(path));
        Table? newTable = Interpret(newFile, new StreamReader(path));
        if (newTable is null)
        {
            return false;
        }
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
        }
        
        await Insert(newTable);
        return true;    
    }

    public async Task<bool> ProcessUploaded(Analyst analyst, IFormFile file)
    {
        if (!await Validate(analyst, file)) 
            return false;
        CSVFile newFile = new CSVFile(analyst.Id, file.FileName);
        Table? newTable = Interpret(newFile, new StreamReader(file.OpenReadStream()));
        if (newTable is null)
        {
            return false;
        }
        foreach (FieldValues fv in newTable.Columns)
        {
            DetectCategory(fv);
        }

        await Insert(newTable);
        return true;  
    }

    public new async Task<CSVFile> GetFile(Analyst analyst, string fileName)
    {
        return await base.GetFile(analyst, fileName);
    }

    public new async Task<CSVFile> GetFile(int csvFileId)
    {
        return await base.GetFile(csvFileId);
    }

    public new async Task<List<CSVFile>> GetAllFiles(Analyst analyst)
    {
        return await base.GetAllFiles(analyst);
    }
    
    public async Task RemoveFile(Analyst analyst, string fileName)
    {
        CSVFile removingFile = await base.GetFile(analyst, fileName);
        await base.RemoveFile(removingFile);
    }

    public async Task RenameFile(Analyst analyst, string fileName, string newFileName)
    {
        CSVFile removingFile = await base.GetFile(analyst, fileName);
        await base.RenameFile(removingFile, newFileName);
    }
    
    public async Task<Table?> GetTableLimited(int csvFileId, int rowLimit)
    {
        CSVFile csvFile = await base.GetFile(csvFileId);
        return await base.GetLimited(csvFile, rowLimit);
    }

    public async Task<TryChangeFieldTypeDTO> ChangeFieldType(int fieldId, string newType, char? listSplitChar)
    {
        if (!TypeNameMap.Map.TryGetValue(newType, out var value)) throw new BadHttpRequestException("We do not deal with this type.");
        return await base.ChangeFieldType(new ChangeFieldTypeDTO(fieldId, value, listSplitChar));
    }
}