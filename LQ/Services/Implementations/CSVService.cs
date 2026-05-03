using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    // ProcessingPayload is written camelCase so the wire format (System.Text.Json defaults
    // for ASP.NET Core MVC) and direct @Html.Raw injection in Razor both work uniformly.
    // CaseInsensitive keeps any pre-existing PascalCase rows readable.
    public static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CSVService>? _logger;

    public CSVService(LQDbContext lqDbContext, IServiceScopeFactory scopeFactory, ILogger<CSVService>? logger = null)
        : base(lqDbContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private static bool Validate(StreamReader reader)
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
        return !await FileExists(analyst, Path.GetFileName(path)) && Validate(new StreamReader(path));
    }

    public async Task<bool> Validate(Analyst analyst, IFormFile file)
    {
        return !await FileExists(analyst, file.FileName) && Validate(new StreamReader(file.OpenReadStream()));
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
        
        // Categorical / Identifier for List<string> — check item uniqueness, not row uniqueness
        if (fieldValues.Field.Type == typeof(List<string>))
        {
            var listItems = fieldValues.Values
                .Where(v => !string.IsNullOrWhiteSpace(v.ValueStringFormatted))
                .SelectMany(v => v.ValueStringFormatted!.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                .ToList();
            double listUniq = listItems.Count > 0
                ? (double)listItems.Distinct().Count() / listItems.Count
                : 0;
            result.Add(listUniq <= Field.CATEGORICAL_THRESHOLD ? Category.Categorical : Category.Identifier);
        }
        else
        {
            // Categorical
            if ((uniqPercentage <= Field.CATEGORICAL_THRESHOLD &&
                (fieldValues.Field.Type == typeof(string) || fieldValues.Field.Type == typeof(int)) ||
                fieldValues.Field.Type == typeof(bool))
                || fieldValues.Field.Type == typeof(DateTime)) result.Add(Category.Categorical);

            // Identifier
            if ((uniqPercentage >= Field.IDENTIFIER_THRESHOLD &&
                 (fieldValues.Field.Type == typeof(string) ||
                  fieldValues.Field.Type == typeof(int))) ||
                (fieldValues.Field.Type == typeof(string) && !result.Contains(Category.Categorical))) result.Add(Category.Identifier);
        }
        
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
    
    private (char separator, List<string> samples)? AuditListDetection(FieldValues fieldvalues)
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
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
                    charCandidates.Add(c);
        }

        if (nonparsedCount == 0 || nonparsedSet.Count == 0) return null;

        double rateNonParsed = (double)nonparsedSet.Count / nonparsedCount;
        if (!charCandidates.Contains(',')) charCandidates.Add(',');

        char bestSeparator = '\0';
        double bestRate = double.MaxValue;
        int bestParsedCount = 0;

        foreach (char sep in charCandidates)
        {
            HashSet<string> parsedSet = new HashSet<string>();
            int parsedCount = 0;

            foreach (Value v in fieldvalues.Values)
            {
                if (string.IsNullOrWhiteSpace(v.ValueString)) continue;
                string s = v.ValueString.Trim().TrimStart(possibleOpen).TrimEnd(possibleEnd);
                foreach (string f in s.Split(sep).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
                {
                    parsedSet.Add(f);
                    parsedCount++;
                }
            }

            if (parsedCount > nonparsedCount)
            {
                double rateParsed = (double)parsedSet.Count / parsedCount;
                // Accept if it would have been caught by either DetectCategoryList or DetectIdentifierList
                if (rateParsed <= Field.IDENTIFIER_LIST_THRESHOLD && rateParsed < rateNonParsed)
                {
                    if (rateParsed < bestRate || (rateParsed == bestRate && parsedCount > bestParsedCount))
                    {
                        bestRate = rateParsed;
                        bestSeparator = sep;
                        bestParsedCount = parsedCount;
                    }
                }
            }
        }

        if (bestSeparator == '\0') return null;

        var samples = fieldvalues.Values
            .Where(v => !string.IsNullOrWhiteSpace(v.ValueString))
            .Take(5)
            .Select(v => v.ValueString!)
            .ToList();

        return (bestSeparator, samples);
    }

    private Type DetectType(FieldValues fieldvalues, bool skipListDetection = false)
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

        if (!skipListDetection && (DetectCategoryList(fieldvalues) || DetectIdentifierList(fieldvalues)))
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
        var splits = new List<string?>();
        int cntQuotes = 0;
        var temp = new System.Text.StringBuilder();
        foreach (var c in line)
        {
            if (c == ',' && cntQuotes == 0)
            {
                splits.Add(temp.ToString());
                temp.Clear();
            }
            else
            {
                temp.Append(c);
                if (c == '"') cntQuotes ^= 1;
            }
        }
        splits.Add(temp.ToString());
        return splits;
    }
    
    private (Table table, List<SuspiciousListDTO> suspicious)? Interpret(CSVFile csvFile, StreamReader reader)
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
            string trimmedfield = fields[i]?.Trim() ?? "";
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

        var suspicious = new List<SuspiciousListDTO>();
        foreach (FieldValues fv in newTable.Columns)
        {
            var type = DetectType(fv, skipListDetection: true);
            if (type == typeof(string))
            {
                var audit = AuditListDetection(fv);
                if (audit.HasValue)
                    suspicious.Add(new SuspiciousListDTO(fv.Field.Index, fv.Field.Name, audit.Value.separator.ToString(), audit.Value.samples));
            }
            fv.Field.Type = type;
        }
        return (newTable, suspicious);
    }

    public async Task<UploadResultDTO?> ProcessUploaded(Analyst analyst, string path)
    {
        if (!await Validate(analyst, path)) return null;
        CSVFile newFile = new CSVFile(analyst.Id, Path.GetFileName(path));
        var result = Interpret(newFile, new StreamReader(path));
        if (result is null) return null;
        var (newTable, suspicious) = result.Value;
        foreach (FieldValues fv in newTable.Columns) DetectCategory(fv);
        await Insert(newTable);
        await FinalizeUploadStatus(newFile, suspicious);
        return new UploadResultDTO(Path.GetFileName(path), newFile.PublicToken, suspicious);
    }

    public async Task<UploadResultDTO?> ProcessUploaded(Analyst analyst, IFormFile file)
    {
        if (!await Validate(analyst, file)) return null;

        // IFormFile is request-scoped — copy the bytes before the request ends.
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Persist the placeholder so the home page can render a Processing card
        // immediately on reload; the heavy work runs in the background.
        var newFile = new CSVFile(analyst.Id, file.FileName);
        await InsertFile(newFile);

        var token = newFile.PublicToken;
        var fileName = file.FileName;
        _ = Task.Run(() => RunInScope(token, bytes, useSinglePass: false));

        return new UploadResultDTO(fileName, token, new List<SuspiciousListDTO>());
    }

    private async Task FinalizeUploadStatus(CSVFile file, List<SuspiciousListDTO> suspicious)
    {
        file.Status = suspicious.Count > 0 ? UploadStatus.AwaitingListConfirmation : UploadStatus.Ready;
        file.ProcessingPayload = suspicious.Count > 0 ? JsonSerializer.Serialize(suspicious, PayloadJson) : null;
        file.ModifiedTime = DateTime.UtcNow;
        await _lqDbContext.SaveChangesAsync();
    }

    private (Table table, List<SuspiciousListDTO> suspicious)? InterpretSinglePass(CSVFile csvFile, StreamReader reader)
    {
        string? headerLine = null;
        while ((headerLine = reader.ReadLine()) != null)
            if (!string.IsNullOrWhiteSpace(headerLine)) break;
        if (headerLine == null) return null;

        var fields = SplitField(headerLine);
        int expectedColumns = fields.Count;
        if (expectedColumns < 2) return null;

        Table newTable = new Table(csvFile);
        for (int i = 0; i < fields.Count; i++)
        {
            string trimmed = fields[i]?.Trim() ?? "";
            Field newField = new Field { Name = trimmed, CSVFile = csvFile, Index = i, Categories = new List<Category>() };
            newTable.Columns.Add(new FieldValues { Field = newField, Values = new List<Value>() });
        }

        int rowIndex = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var splits = SplitField(line);
            if (splits.Count != expectedColumns) return null;
            newTable.AddRow(rowIndex++, splits);
        }

        if (rowIndex == 0) return null;

        var suspiciousBag = new ConcurrentBag<SuspiciousListDTO>();
        Parallel.ForEach(newTable.Columns, fv =>
        {
            var type = DetectType(fv, skipListDetection: true);
            if (type == typeof(string))
            {
                var audit = AuditListDetection(fv);
                if (audit.HasValue)
                    suspiciousBag.Add(new SuspiciousListDTO(fv.Field.Index, fv.Field.Name, audit.Value.separator.ToString(), audit.Value.samples));
            }
            fv.Field.Type = type;
            DetectCategory(fv);
        });
        return (newTable, suspiciousBag.OrderBy(s => s.FieldIndex).ToList());
    }

    public async Task<UploadResultDTO?> ProcessUploadedGuest(string guestToken, IFormFile file)
    {
        if (await FileExists(guestToken, file.FileName)) return null;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var newFile = new CSVFile(guestToken, file.FileName, true);
        await InsertFile(newFile);

        var token = newFile.PublicToken;
        var fileName = file.FileName;
        _ = Task.Run(() => RunInScope(token, bytes, useSinglePass: true));

        return new UploadResultDTO(fileName, token, new List<SuspiciousListDTO>());
    }

    // Resolves a fresh ICSVService in a new DI scope and dispatches to the
    // continuation. Kept here so the kickoff paths don't leak scope-management
    // detail into the controller.
    private async Task RunInScope(Guid token, byte[] bytes, bool useSinglePass)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ICSVService>();
            await svc.ContinueProcessAsync(token, bytes, useSinglePass);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Background upload processing failed for {Token}", token);
        }
    }

    public async Task ContinueProcessAsync(Guid token, byte[] fileBytes, bool useSinglePass)
    {
        var file = await _lqDbContext.CSVFiles.FirstOrDefaultAsync(f => f.PublicToken == token);
        if (file is null) return;

        try
        {
            using var ms = new MemoryStream(fileBytes);
            using var reader = new StreamReader(ms);
            var result = useSinglePass ? InterpretSinglePass(file, reader) : Interpret(file, reader);
            if (result is null)
            {
                file.Status = UploadStatus.Failed;
                file.ProcessingPayload = JsonSerializer.Serialize(new { error = "Failed to parse CSV" }, PayloadJson);
                file.ModifiedTime = DateTime.UtcNow;
                await _lqDbContext.SaveChangesAsync();
                return;
            }

            var (newTable, suspicious) = result.Value;
            // InterpretSinglePass already runs DetectCategory inside its parallel loop.
            if (!useSinglePass)
                foreach (var fv in newTable.Columns) DetectCategory(fv);

            await InsertFieldsAndValues(newTable);
            await FinalizeUploadStatus(file, suspicious);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ContinueProcessAsync failed for {Token}", token);
            file.Status = UploadStatus.Failed;
            file.ProcessingPayload = JsonSerializer.Serialize(new { error = ex.Message }, PayloadJson);
            file.ModifiedTime = DateTime.UtcNow;
            try { await _lqDbContext.SaveChangesAsync(); } catch { /* swallow — surface via Failed status */ }
        }
    }

    public async Task<bool> ConfirmListFields(Guid token, string? analystId, string? guestToken, List<ConfirmFieldDTO> confirmed)
    {
        var file = await _lqDbContext.CSVFiles.FirstOrDefaultAsync(f => f.PublicToken == token);
        if (file is null) return false;
        if (analystId is not null && file.AnalystID != analystId) return false;
        if (analystId is null && file.GuestToken != guestToken) return false;

        foreach (var c in confirmed)
        {
            var field = await _lqDbContext.Fields
                .FirstOrDefaultAsync(f => f.CSVFile.ID == file.ID && f.Index == c.FieldIndex);
            if (field is null) continue;
            char sep = c.Separator.Length == 1 ? c.Separator[0] : ',';
            await ChangeFieldType(new ChangeFieldTypeDTO(field.ID, typeof(List<string>), sep));
        }

        file.Status = UploadStatus.Ready;
        file.ProcessingPayload = null;
        file.ModifiedTime = DateTime.UtcNow;
        await _lqDbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<PendingUploadDTO>> GetPendingUploads(string? analystId, string? guestToken)
    {
        IQueryable<CSVFile> query = _lqDbContext.CSVFiles.Where(f => f.Status != UploadStatus.Ready);
        query = analystId is not null
            ? query.Where(f => f.AnalystID == analystId)
            : guestToken is not null
                ? query.Where(f => f.GuestToken == guestToken)
                : query.Where(_ => false);

        var rows = await query.OrderByDescending(f => f.CreatedTime).ToListAsync();
        return rows.Select(f =>
        {
            List<SuspiciousListDTO>? suspicious = null;
            string? error = null;
            if (f.ProcessingPayload is not null)
            {
                try
                {
                    if (f.Status == UploadStatus.AwaitingListConfirmation)
                        suspicious = JsonSerializer.Deserialize<List<SuspiciousListDTO>>(f.ProcessingPayload, PayloadJson);
                    else if (f.Status == UploadStatus.Failed)
                        error = JsonSerializer.Deserialize<JsonElement>(f.ProcessingPayload)
                            .GetProperty("error").GetString();
                }
                catch (JsonException) { /* malformed payload — surface as null */ }
            }
            return new PendingUploadDTO(f.PublicToken, f.Name, f.Status.ToString(), suspicious, error);
        }).ToList();
    }

    public async Task<CSVFile> GetFileGuest(string guestToken, string fileName)
        => await base.GetFile(guestToken, fileName);

    public async Task<List<CSVFile>> GetAllFilesGuest(string guestToken)
        => await base.GetAllFiles(guestToken);

    public async Task RemoveFileGuest(string guestToken, string fileName)
        => await base.RemoveFileByGuest(guestToken, fileName);

    public new async Task<CSVFile> GetFile(Analyst analyst, string fileName)
    {
        return await base.GetFile(analyst, fileName);
    }

    public new async Task<CSVFile> GetFile(int csvFileId)
    {
        return await base.GetFile(csvFileId);
    }

    public async Task<CSVFile?> GetFile(Guid publicToken)
    {
        return await _lqDbContext.CSVFiles.FirstOrDefaultAsync(f => f.PublicToken == publicToken);
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

    public async Task<TableDisplayDTO> GetTableDisplayLimited(CSVFile csvFile, int rowLimit)
    {
        return await base.GetDisplayLimited(csvFile, rowLimit);
    }

    public async Task<TableDisplayDTO> GetTableDisplayWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit)
    {
        return await base.GetDisplayWithFilterLimited(csvFile, filters, rowLimit);
    }

    protected override async Task<TryChangeFieldTypeDTO> ChangeFieldType(ChangeFieldTypeDTO dto)
    {
        var result = await base.ChangeFieldType(dto);
        if (!result.Status || dto.NewType != typeof(List<string>)) return result;

        char sep = dto.ListSplitChar ?? ',';
        var rawValues = await _lqDbContext.Values
            .Where(v => v.FieldID == dto.FieldId && !string.IsNullOrWhiteSpace(v.ValueString))
            .Select(v => v.ValueString!)
            .ToListAsync();

        var allItems = rawValues
            .SelectMany(v => v.Trim().Split(sep).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)))
            .ToList();

        double uniqueRatio = allItems.Count > 0
            ? (double)allItems.Distinct().Count() / allItems.Count
            : 0;

        var field = await _lqDbContext.Fields.SingleAsync(f => f.ID == dto.FieldId);
        field.Categories = uniqueRatio <= Field.CATEGORICAL_THRESHOLD
            ? [Category.Categorical]
            : [Category.Identifier];
        await _lqDbContext.SaveChangesAsync();

        return result;
    }

    public async Task<TryChangeFieldTypeDTO> ChangeFieldType(CSVFile csvFile, int fieldIndex, string newType, char? listSplitChar)
    {
        if (!TypeNameMap.Map.TryGetValue(newType, out var value)) throw new BadHttpRequestException("We do not deal with this type.");
        var field = await base.GetField(csvFile.ID, fieldIndex);
        return await ChangeFieldType(new ChangeFieldTypeDTO(field.ID, value, listSplitChar));
    }

    public async Task<string> ChangeFieldCategories(CSVFile csvFile, int fieldIndex, List<Category> newCategories)
    {
        var field = await base.GetField(csvFile.ID, fieldIndex);
        field.Categories = newCategories.Distinct().ToList();
        await _lqDbContext.SaveChangesAsync();
        return "Categories updated";
    }
}