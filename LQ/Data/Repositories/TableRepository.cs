using EFCore.BulkExtensions;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;

namespace LQ.Data.Repositories;

public class TableRepository : TableRepositoryAbstract
{
    protected readonly LQDbContext _lqDbContext;

    public TableRepository(LQDbContext lqDbContext)
    {
        _lqDbContext = lqDbContext;
    }

    protected override async Task InsertFile(CSVFile csvFile)
    {
        _lqDbContext.CSVFiles.Add(csvFile);
        await _lqDbContext.SaveChangesAsync();
    }

    public override async Task<CSVFile> GetFile(Analyst analyst, string fileName)
    {
        return await _lqDbContext.CSVFiles.SingleAsync(f =>
            f.AnalystID == analyst.Id && f.Name == fileName);
    }

    public override async Task<CSVFile> GetFile(int csvFileId)
    {
        return await _lqDbContext.CSVFiles.SingleAsync(f => f.ID == csvFileId);
    }

    public override async Task<List<CSVFile>> GetAllFiles(Analyst analyst)
    {
        return await _lqDbContext.CSVFiles
            .Where(f => f.AnalystID == analyst.Id)
            .ToListAsync();
    }

    public override Task<bool> FileExists(Analyst analyst, string fileName)
    {
        return _lqDbContext.CSVFiles
            .AnyAsync(f => f.AnalystID == analyst.Id && f.Name == fileName);
    }

    public override async Task<CSVFile> GetFile(string guestToken, string fileName)
        => await _lqDbContext.CSVFiles.SingleAsync(f => f.GuestToken == guestToken && f.Name == fileName);

    public override async Task<List<CSVFile>> GetAllFiles(string guestToken)
        => await _lqDbContext.CSVFiles.Where(f => f.GuestToken == guestToken).ToListAsync();
    
    public override async Task<bool> FileExists(string guestToken, string fileName)
        => await _lqDbContext.CSVFiles.AnyAsync(f => f.GuestToken == guestToken && f.Name == fileName);

    protected override async Task RemoveFileByGuest(string guestToken, string fileName)
        => await _lqDbContext.CSVFiles.Where(f => f.GuestToken == guestToken && f.Name == fileName)
            .ExecuteDeleteAsync();
    
    protected override async Task RemoveFile(CSVFile csvFile)
    {
        await _lqDbContext.CSVFiles
            .Where(f => f.ID == csvFile.ID)
            .ExecuteDeleteAsync();
    }

    protected override async Task RenameFile(CSVFile csvFile, string newName)
    {
        await _lqDbContext.CSVFiles
            .Where(f => f.ID == csvFile.ID)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, newName));
    }

    protected override async Task Insert(Table table)
    {
        await InsertFile(table.CSVFile);
        await InsertFieldsAndValues(table);
    }

    // For the async-upload kickoff path: the CSVFile placeholder is persisted
    // up-front so a card can render immediately, and the background task only
    // needs to add fields + values to the already-tracked file.
    protected async Task InsertFieldsAndValues(Table table)
    {
        await _lqDbContext.Fields.AddRangeAsync(table.Columns.Select(f => f.Field));
        await _lqDbContext.SaveChangesAsync();

        foreach (var col in table.Columns)
        {
            var fieldId = col.Field.ID;
            foreach (var v in col.Values)
            {
                v.FieldID = fieldId;
            }
        }

        var allValues = table.Columns.SelectMany(f => f.Values).ToList();
        await BulkInsertValuesAsync(allValues);
    }

    public override async Task<Table> Get(CSVFile csvFile)
    {
        var fields = await GetFieldsAsync(csvFile.ID);
        var fieldIds = fields.Select(f => f.ID).ToList();

        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID))
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, fields, values);
    }

    public override async Task<Table> Get(CSVFile csvFile, List<int> fieldIndexes)
    {
        var fields = await GetFieldsAsync(csvFile.ID, fieldIndexes);
        var fieldIds = fields.Select(f => f.ID).ToList();

        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID))
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, fields, values);
    }

    public override async Task<List<Field>> GetFields(CSVFile csvFile, List<int> fieldIndexes)
        => await GetFieldsAsync(csvFile.ID, fieldIndexes);

    public override async Task<Field> GetField(int csvFileId, int fieldIndex)
        => await GetFieldsQuery(csvFileId, [fieldIndex]).SingleAsync();

    public override async Task<Table> GetLimited(CSVFile csvFile, int rowLimit)
    {
        var fields = await GetFieldsAsync(csvFile.ID);
        var fieldIds = fields.Select(f => f.ID).ToList();

        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID) && v.RowIndex < rowLimit)
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, fields, values);
    }

    public override async Task<Table> GetLimited(CSVFile csvFile, List<int> fieldIndexes, int rowLimit)
    {
        var fields = await GetFieldsAsync(csvFile.ID, fieldIndexes);
        var fieldIds = fields.Select(f => f.ID).ToList();

        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID) && v.RowIndex < rowLimit)
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, fields, values);
    }

    public override async Task<Table> GetWithFilter(CSVFile csvFile, List<ValueFilterDTO> filters)
    {
        var fieldsQuery = GetFieldsQuery(csvFile.ID);
        var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
        var validRowIndexes = GetFilteredRowIndexesQuery(csvFile.ID, filters);

        var values = await _lqDbContext.Values
            .Where(v => fieldIdsQuery.Contains(v.FieldID) && validRowIndexes.Contains(v.RowIndex))
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, await fieldsQuery.ToListAsync(), values);
    }

    public override async Task<Table> GetWithFilter(CSVFile csvFile, List<int> fieldIndexes,
        List<ValueFilterDTO> filters)
    {
        var fieldsQuery = GetFieldsQuery(csvFile.ID, fieldIndexes);
        var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
        var validRowIndexes = GetFilteredRowIndexesQuery(csvFile.ID, filters);

        var values = await _lqDbContext.Values
            .Where(v => fieldIdsQuery.Contains(v.FieldID) && validRowIndexes.Contains(v.RowIndex))
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, await fieldsQuery.ToListAsync(), values);
    }

    public override async Task<Table> GetWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit)
    {
        var fieldsQuery = GetFieldsQuery(csvFile.ID);
        var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
        var limitedRowIndexes = GetFilteredRowIndexesQuery(csvFile.ID, filters)
            .OrderBy(r => r)
            .Take(rowLimit);

        var values = await _lqDbContext.Values
            .Where(v => fieldIdsQuery.Contains(v.FieldID) && limitedRowIndexes.Contains(v.RowIndex))
            .OrderBy(v => v.RowIndex)
            .ToListAsync();

        return BuildTable(csvFile, await fieldsQuery.ToListAsync(), values);
    }

    public override async Task<TableDisplayDTO> GetDisplayLimited(CSVFile csvFile, int rowLimit)
    {
        var fields = await GetFieldsAsync(csvFile.ID);
        var fieldsDisplay = fields.Select(f => new TableFieldDTO
        {
            Name = f.Name ?? "",
            TypeName = f.Type.Name ?? "",
            Categories = f.Categories,
            Index = f.Index,
        }).ToList();
        var fieldIds = fields.Select(f => f.ID).ToHashSet();
        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID) && v.RowIndex < rowLimit)
            .GroupBy(v => v.RowIndex)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(v => v.FieldID)
                .Select(v => v.ValueStringFormatted)
                .ToArray())
            .ToArrayAsync();
        return new TableDisplayDTO
        {
            FileName = csvFile.Name,
            Fields = fieldsDisplay,
            Values = values,
        };
    }
    
    public override async Task<TableDisplayDTO> GetDisplayWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters,
        int rowLimit)
    {
        var fields = await GetFieldsAsync(csvFile.ID);
        var fieldsDisplay = fields.Select(f => new TableFieldDTO
        {
            Name = f.Name ?? "",
            TypeName = f.Type.Name ?? "",
            Categories = f.Categories,
            Index = f.Index,
        }).ToList();
        var fieldIds = fields.Select(f => f.ID).ToHashSet();
        var limitedRowIndexes = GetFilteredRowIndexesQuery(csvFile.ID, filters)
            .OrderBy(r => r)
            .Take(rowLimit);
        var values = await _lqDbContext.Values
            .Where(v => fieldIds.Contains(v.FieldID) && limitedRowIndexes.Contains(v.RowIndex))
            .GroupBy(v => v.RowIndex)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(v => v.FieldID)
                .Select(v => v.ValueStringFormatted)
                .ToArray())
            .ToArrayAsync();
        return new TableDisplayDTO
        {
            FileName = csvFile.Name,
            Fields = fieldsDisplay,
            Values = values,
        };
    }

    // ─── Field type change ────────────────────────────────────────────────────

    protected override async Task<TryChangeFieldTypeDTO> ChangeFieldType(ChangeFieldTypeDTO dto)
    {
        var field = await _lqDbContext.Fields.SingleOrDefaultAsync(f => f.ID == dto.FieldId)
                    ?? throw new BadHttpRequestException("Field not found.");

        var (tryResult, plan) = await TryChangeFieldType(field, dto.NewType);
        if (!tryResult.Status) return tryResult;
        if (field.Type == dto.NewType) return tryResult;

        await using var tx = await _lqDbContext.Database.BeginTransactionAsync();

        field.Type = dto.NewType;

        if (dto.NewType == typeof(DateTime))
        {
            await ReformatDateTimeValues(dto.FieldId, plan!.Unit);
            field.Categories = new List<Category> { Category.Categorical };
        }
        else if (dto.NewType == typeof(List<string>))
            await ReformatListValues(dto);
        
        await _lqDbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return tryResult;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static Table BuildTable(CSVFile csvFile, List<Field> fields, List<Value> values)
    {
        var valuesByFieldId = values
            .GroupBy(v => v.FieldID)
            .ToDictionary(g => g.Key, g => g.ToList());

        var table = new Table(csvFile);
        foreach (var field in fields)
            table.Columns.Add(new FieldValues
            {
                Field = field,
                Values = valuesByFieldId.GetValueOrDefault(field.ID, new List<Value>())
            });

        table.SetRowCount();
        return table;
    }

    private IQueryable<Field> GetFieldsQuery(int csvFileId, List<int>? fieldIndexes = null)
    {
        var query = _lqDbContext.Fields.Where(f => f.CSVFile.ID == csvFileId);

        if (fieldIndexes != null)
            query = query.Where(f => fieldIndexes.Contains(f.Index));

        return query.OrderBy(f => f.Index);
    }

    private async Task<List<Field>> GetFieldsAsync(int csvFileId, List<int>? fieldIndexes = null)
        => await GetFieldsQuery(csvFileId, fieldIndexes).ToListAsync();

    private IQueryable<int> GetFilteredRowIndexesQuery(int csvFileId, List<ValueFilterDTO> filters)
    {
        if (filters.Count == 0)
            return _lqDbContext.Values
                .Where(v => v.Field.CSVFile.ID == csvFileId)
                .Select(v => v.RowIndex)
                .Distinct();

        IQueryable<int>? result = null;

        foreach (var filter in filters)
        {
            var filterQuery = _lqDbContext.Values
                .Where(v => v.Field.CSVFile.ID == csvFileId && v.Field.Index == filter.FieldIndex);

            filterQuery = ApplyFilter(filterQuery, filter);

            var matchingRows = filterQuery.Select(v => v.RowIndex);
            result = result == null ? matchingRows : result.Intersect(matchingRows);
        }

        return result!;
    }

    private static IQueryable<Value> ApplyFilter(IQueryable<Value> query, ValueFilterDTO filter)
    {
        switch (filter.FieldType)
        {
            case FilterFieldType.Int:
                query = query.Where(v => v.ValueStringFormatted != null && v.ValueStringFormatted != "");
                if (filter.FilteringValuesInt?.Count > 0)
                    query = filter.IsNot
                        ? query.Where(v => !filter.FilteringValuesInt.Contains(Convert.ToInt32(v.ValueStringFormatted)))
                        : query.Where(v => filter.FilteringValuesInt.Contains(Convert.ToInt32(v.ValueStringFormatted)));
                if (filter.IntBound != null)
                {
                    var b = filter.IntBound.Value;
                    query = filter.IsNot
                        ? query.Where(v =>
                            !(Convert.ToInt32(v.ValueStringFormatted) >= b.leftBound &&
                              Convert.ToInt32(v.ValueStringFormatted) <= b.rightBound))
                        : query.Where(v =>
                            Convert.ToInt32(v.ValueStringFormatted) >= b.leftBound &&
                            Convert.ToInt32(v.ValueStringFormatted) <= b.rightBound);
                }

                break;

            case FilterFieldType.Double:
                query = query.Where(v => v.ValueStringFormatted != null && v.ValueStringFormatted != "");
                if (filter.FilteringValuesDouble?.Count > 0)
                    query = filter.IsNot
                        ? query.Where(v =>
                            !filter.FilteringValuesDouble.Contains(Convert.ToDouble(v.ValueStringFormatted)))
                        : query.Where(v =>
                            filter.FilteringValuesDouble.Contains(Convert.ToDouble(v.ValueStringFormatted)));
                if (filter.DoubleBound != null)
                {
                    var b = filter.DoubleBound.Value;
                    query = filter.IsNot
                        ? query.Where(v =>
                            !(Convert.ToDouble(v.ValueStringFormatted) >= b.leftBound &&
                              Convert.ToDouble(v.ValueStringFormatted) <= b.rightBound))
                        : query.Where(v =>
                            Convert.ToDouble(v.ValueStringFormatted) >= b.leftBound &&
                            Convert.ToDouble(v.ValueStringFormatted) <= b.rightBound);
                }

                break;

            case FilterFieldType.DateTime:
                if (filter.DateTimeBound != null)
                {
                    var bound = filter.DateTimeBound.Value;
                    var matchingRows = query
                        .Select(v => new { v.RowIndex, v.ValueStringFormatted })
                        .AsEnumerable()
                        .Where(v => !string.IsNullOrEmpty(v.ValueStringFormatted)
                                    && DateTime.TryParse(v.ValueStringFormatted, out var dt)
                                    && (filter.IsNot
                                        ? !(dt >= bound.leftBound && dt <= bound.rightBound)
                                        : dt >= bound.leftBound && dt <= bound.rightBound))
                        .Select(v => v.RowIndex)
                        .ToList();

                    query = query.Where(v => matchingRows.Contains(v.RowIndex));
                }

                break;

            case FilterFieldType.ListString:
                if (filter.FilteringValues?.Count > 0)
                    query = query.Where(BuildStringExpression(filter.FilteringValues, filter.IsNot,
                        filter.IsPartialMatch, isListField: true));
                break;

            case FilterFieldType.Bool:
                if (filter.FilteringValues?.Count > 0)
                    query = filter.IsNot
                        ? query.Where(v => !filter.FilteringValues.Contains(v.ValueStringFormatted))
                        : query.Where(v => filter.FilteringValues.Contains(v.ValueStringFormatted));
                break;

            case FilterFieldType.String:
                if (filter.FilteringValues?.Count > 0)
                    query = query.Where(BuildStringExpression(filter.FilteringValues, filter.IsNot,
                        filter.IsPartialMatch, isListField: false));
                break;
        }

        return query;
    }

    private static Expression<Func<Value, bool>> BuildStringExpression(
        List<string> values, bool isNot, bool isPartial, bool isListField)
    {
        var param = Expression.Parameter(typeof(Value), "v");
        var property = Expression.Property(param,
            isListField ? nameof(Value.ValueStringFormatted) : nameof(Value.ValueString));
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;

        Expression? aggregate = null;
        foreach (var s in values)
        {
            Expression check = isPartial || isListField
                ? Expression.Call(property, containsMethod, Expression.Constant(s))
                : Expression.Equal(property, Expression.Constant(s));

            aggregate = aggregate == null
                ? check
                : isPartial
                    ? Expression.OrElse(aggregate, check)
                    : Expression.AndAlso(aggregate, check);
        }

        if (aggregate == null) return v => false;

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        Expression combined = isNot
            ? Expression.Not(Expression.AndAlso(nullCheck, aggregate))
            : Expression.AndAlso(nullCheck, aggregate);

        return Expression.Lambda<Func<Value, bool>>(combined, param);
    }

    // ─── Field type change: validation + reformat ─────────────────────────────

    private enum DateTimeUnit { Native, UnixSeconds, UnixMilliseconds, UnixMicroseconds, UnixNanoseconds }

    private sealed record DateTimeParseResult(DateTimeUnit Unit, List<string> Mismatches);

    // Year 2200-01-01 UTC, expressed in each Unix unit.
    private const long Year2200UnixSeconds      = 7_258_118_400L;
    private const long Year2200UnixMilliseconds = 7_258_118_400_000L;
    private const long Year2200UnixMicroseconds = 7_258_118_400_000_000L;

    private async Task<(TryChangeFieldTypeDTO Dto, DateTimeParseResult? Plan)> TryChangeFieldType(Field field, Type newType)
    {
        if (field.Type == newType) return (new TryChangeFieldTypeDTO(null), null);

        var rawValues = await _lqDbContext.Values
            .Where(v => v.Field == field && v.ValueStringFormatted != null)
            .OrderBy(v => v.RowIndex)
            .Select(v => v.ValueStringFormatted!)
            .ToListAsync();

        if (newType == typeof(DateTime))
        {
            var plan = DetectDateTimeStrategy(rawValues);
            return (new TryChangeFieldTypeDTO(plan.Mismatches), plan);
        }

        var mismatches = rawValues
            .Where(val => !CanConvert(val, newType))
            .ToList();

        return (new TryChangeFieldTypeDTO(mismatches), null);
    }

    private static bool CanConvert(string value, Type targetType) => targetType switch
    {
        _ when targetType == typeof(string) => true,
        _ when targetType == typeof(int) => int.TryParse(value, out _),
        _ when targetType == typeof(double) => double.TryParse(value, out _),
        _ when targetType == typeof(List<string>) => true,
        _ => false
    };

    /// <summary>
    /// Picks a single DateTime parsing strategy for the column.
    /// Tries DateTime.TryParse first; if that fails on any value, falls back to Unix-epoch
    /// numerics and chooses the smallest unit (s / ms / µs / ns) whose max value lands at or
    /// before year 2200. Values that don't land in [1900, 2200] under the chosen strategy are
    /// returned as mismatches. Negative numerics are rejected.
    /// Mixed-unit columns (some seconds + some ms) cannot be recovered automatically; the
    /// chosen unit is applied uniformly and the user must clean their data.
    /// </summary>
    private static DateTimeParseResult DetectDateTimeStrategy(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return new DateTimeParseResult(DateTimeUnit.Native, new List<string>());

        var nativeMismatches = new List<string>();
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (!DateTime.TryParse(v, out _)) nativeMismatches.Add(v);
        }
        if (nativeMismatches.Count == 0)
            return new DateTimeParseResult(DateTimeUnit.Native, new List<string>());

        double max = double.MinValue;
        bool anyNumeric = false;
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0)
            {
                anyNumeric = true;
                if (d > max) max = d;
            }
        }

        if (!anyNumeric)
            return new DateTimeParseResult(DateTimeUnit.Native, nativeMismatches);

        DateTimeUnit unit =
            max <= Year2200UnixSeconds      ? DateTimeUnit.UnixSeconds      :
            max <= Year2200UnixMilliseconds ? DateTimeUnit.UnixMilliseconds :
            max <= Year2200UnixMicroseconds ? DateTimeUnit.UnixMicroseconds :
                                              DateTimeUnit.UnixNanoseconds;

        var rangeMismatches = new List<string>();
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (!TryToDateTime(v, unit, out var dt) || dt.Year < 1900 || dt.Year > 2200)
                rangeMismatches.Add(v);
        }

        return new DateTimeParseResult(unit, rangeMismatches);
    }

    private static bool TryToDateTime(string value, DateTimeUnit unit, out DateTime result)
    {
        result = default;

        if (unit == DateTimeUnit.Native)
            return DateTime.TryParse(value, out result);

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < 0)
            return false;

        try
        {
            result = unit switch
            {
                DateTimeUnit.UnixSeconds      => DateTime.UnixEpoch.AddSeconds(d),
                DateTimeUnit.UnixMilliseconds => DateTime.UnixEpoch.AddMilliseconds(d),
                DateTimeUnit.UnixMicroseconds => DateTime.UnixEpoch.AddTicks((long)(d * 10)),
                DateTimeUnit.UnixNanoseconds  => DateTime.UnixEpoch.AddTicks((long)(d / 100)),
                _ => default
            };
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (OverflowException) { return false; }
    }

    private async Task ReformatDateTimeValues(int fieldId, DateTimeUnit unit)
    {
        var values = await _lqDbContext.Values
            .Where(v => v.FieldID == fieldId)
            .AsNoTracking()
            .ToListAsync();

        var enUS = CultureInfo.GetCultureInfo("en-US");

        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v.ValueStringFormatted))
            {
                v.ValueStringFormatted = null;
                continue;
            }

            v.ValueStringFormatted = TryToDateTime(v.ValueStringFormatted, unit, out var dt)
                ? dt.ToString(enUS)
                : null;
        }

        await _lqDbContext.BulkUpdateAsync(values, new BulkConfig
        {
            PropertiesToInclude = new List<string> { nameof(Value.ValueStringFormatted) }
        });
    }

    private async Task ReformatListValues(ChangeFieldTypeDTO dto)
    {
        if (dto.ListSplitChar is null)
            throw new BadHttpRequestException("Null list split character.");

        var values = await _lqDbContext.Values
            .Where(v => v.FieldID == dto.FieldId)
            .AsNoTracking()
            .ToListAsync();

        foreach (var v in values)
        {
            v.ValueStringFormatted = string.IsNullOrWhiteSpace(v.ValueString)
                ? null
                : string.Join(',',
                    v.ValueString
                        .Trim()
                        .TrimStart(CSVService.possibleOpen)
                        .TrimEnd(CSVService.possibleEnd)
                        .Split((char)dto.ListSplitChar)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s)));
        }

        await _lqDbContext.BulkUpdateAsync(values, new BulkConfig
        {
            PropertiesToInclude = new List<string> { nameof(Value.ValueStringFormatted) }
        });
    }

    private async Task BulkInsertValuesAsync(List<Value> values)
    {
        if (values.Count == 0) return;
        await _lqDbContext.BulkInsertAsync(values, new BulkConfig
        {
            BatchSize = 10_000,
            PreserveInsertOrder = true,
            SetOutputIdentity = true
        });
    }

    // ─── Row-filter helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a SQL AND-clause that restricts to the given row indexes.
    /// Safe for integer-only arrays (no injection risk).
    /// Returns an empty string when rowFilter is null or empty (no restriction).
    /// </summary>
    private static string RF(int[]? rowFilter, string alias = "v") =>
        rowFilter is { Length: > 0 }
            ? $"AND {alias}.\"RowIndex\" = ANY(ARRAY[{string.Join(",", rowFilter)}])"
            : "";

    private static bool IsListField(Field f) => f.Type == typeof(List<string>);

    /// <summary>
    /// Returns a PostgreSQL expression that parses a .NET US-culture DateTime string
    /// ("M/D/YYYY H:MI:SS AM/PM") into a TIMESTAMP.
    /// Normalises chr(8239) (U+202F narrow no-break space, used by .NET 6+ en-US before AM/PM)
    /// and chr(160) (U+00A0 NBSP) to regular space before splitting, then uses MAKE_TIMESTAMP
    /// with manual AM/PM → 24 h conversion to avoid all TO_TIMESTAMP format-string issues.
    /// </summary>
    private static string ParseDT(string col)
    {
        // Normalise any Unicode space variants to regular ASCII space so SPLIT_PART works correctly.
        var s = $"REPLACE(REPLACE(TRIM({col}), chr(8239), ' '), chr(160), ' ')";
        return $"""
            MAKE_TIMESTAMP(
                SPLIT_PART(SPLIT_PART({s}, ' ', 1), '/', 3)::int,
                SPLIT_PART(SPLIT_PART({s}, ' ', 1), '/', 1)::int,
                SPLIT_PART(SPLIT_PART({s}, ' ', 1), '/', 2)::int,
                CASE
                    WHEN UPPER(TRIM(SPLIT_PART({s}, ' ', 3))) = 'PM'
                         AND SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 1)::int < 12
                    THEN SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 1)::int + 12
                    WHEN UPPER(TRIM(SPLIT_PART({s}, ' ', 3))) = 'AM'
                         AND SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 1)::int = 12
                    THEN 0
                    ELSE SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 1)::int
                END,
                SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 2)::int,
                SPLIT_PART(SPLIT_PART({s}, ' ', 2), ':', 3)::double precision
            )
            """;
    }

    /// <summary>
    /// Returns the SELECT/GROUP BY key expression and the LATERAL UNNEST clause
    /// to append after the FROM for a field used as a grouping key.
    /// For regular fields returns the plain column reference and an empty lateral.
    /// For List&lt;string&gt; fields, unnests the comma-separated value into individual items.
    /// </summary>
    private static (string KeyExpr, string Lateral) ListExpr(Field f, string unnestAlias) =>
        IsListField(f)
            ? ($"TRIM({unnestAlias})",
               $", LATERAL UNNEST(string_to_array(\"Value_{f.ID}\", ',')) AS {unnestAlias}")
            : ($"\"Value_{f.ID}\"", "");

    public override async Task<int[]> GetFilteredRowIndexes(CSVFile csvFile, List<ValueFilterDTO> filters)
    {
        if (filters.Count == 0) return [];
        return await GetFilteredRowIndexesQuery(csvFile.ID, filters).ToArrayAsync();
    }

    // ─── Scale helpers ────────────────────────────────────────────────────────

    public override async Task<int> CountDistinct(Field field, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return 0;
        var rf = RF(rowFilter);
        string sql;
        if (IsListField(field))
        {
            sql = $"""
                SELECT COUNT(DISTINCT TRIM(cat))::int AS "Value"
                FROM "app"."Value" v,
                     LATERAL UNNEST(string_to_array(v."ValueStringFormatted", ',')) AS cat
                WHERE v."FieldID" = {field.ID}
                  AND v."ValueStringFormatted" IS NOT NULL
                  {rf}
                """;
        }
        else
        {
            sql = $"""
                SELECT COUNT(DISTINCT v."ValueStringFormatted")::int AS "Value"
                FROM "app"."Value" v
                WHERE v."FieldID" = {field.ID}
                  AND v."ValueStringFormatted" IS NOT NULL
                  {rf}
                """;
        }
        return await _lqDbContext.Database.SqlQueryRaw<int>(sql).FirstAsync();
    }

    public override async Task<(double Median, double IQR)> GetScaleInfo(Field numericField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return (0, 1);
        var rf = RF(rowFilter);
        var sql = $"""
            SELECT
                COALESCE(PERCENTILE_CONT(0.5)  WITHIN GROUP (ORDER BY CAST(v."ValueStringFormatted" AS DOUBLE PRECISION)), 0) AS "Median",
                COALESCE(PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY CAST(v."ValueStringFormatted" AS DOUBLE PRECISION))
                       - PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY CAST(v."ValueStringFormatted" AS DOUBLE PRECISION)), 1) AS "IQR"
            FROM "app"."Value" v
            WHERE v."FieldID" = {numericField.ID}
              AND v."ValueStringFormatted" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
              {rf}
            """;
        var row = await _lqDbContext.Database.SqlQueryRaw<ScaleInfoRow>(sql).FirstAsync();
        return (row.Median, Math.Max(row.IQR, 1e-10));
    }

    public override async Task<(DateTime Min, DateTime Max)> GetDateRange(Field dateField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return (DateTime.UtcNow, DateTime.UtcNow);
        var rf = RF(rowFilter);
        var dt = ParseDT(@"v.""ValueStringFormatted""");
        var sql = $"""
            SELECT
                MIN({dt}) AS "Min",
                MAX({dt}) AS "Max"
            FROM "app"."Value" v
            WHERE v."FieldID" = {dateField.ID}
              AND v."ValueStringFormatted" IS NOT NULL
              AND v."ValueStringFormatted" != ''
              {rf}
            """;
        var row = await _lqDbContext.Database.SqlQueryRaw<DateRangeRow>(sql).FirstAsync();
        return (DateTime.SpecifyKind(row.Min, DateTimeKind.Utc), DateTime.SpecifyKind(row.Max, DateTimeKind.Utc));
    }

    public override async Task<SortedDictionary<int, int>> GetHistogramBuckets(Field numericField, double min, double max, int numBuckets, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<int, int>();
        // WIDTH_BUCKET returns 0 for values below min and numBuckets+1 for values at/above max.
        // Expand max by a tiny epsilon so the maximum value falls in the last real bucket.
        double high = max + Math.Abs(max) * 1e-9 + 1e-12;
        var rf = RF(rowFilter);
        var sql = $"""
            SELECT WIDTH_BUCKET(CAST(v."ValueStringFormatted" AS DOUBLE PRECISION), {min.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {high.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {numBuckets}) AS "Bucket",
                   COUNT(*)::int AS "Count"
            FROM "app"."Value" v
            WHERE v."FieldID" = {numericField.ID}
              AND v."ValueStringFormatted" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
              {rf}
            GROUP BY 1
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<HistBucketRow>(sql).ToListAsync();
        var result = new SortedDictionary<int, int>();
        foreach (var r in rows)
            if (r.Bucket >= 1 && r.Bucket <= numBuckets)
                result[r.Bucket] = r.Count;
        return result;
    }

    private record DateRangeRow  { public DateTime Min { get; init; } public DateTime Max { get; init; } }
    private record HistBucketRow { public int Bucket  { get; init; } public int Count   { get; init; } }
    private record ScaleInfoRow  { public double Median { get; init; } public double IQR { get; init; } }

    public override async Task<List<(object Key, double Sum)>> Sum(Field fieldNumeric, params Field[] fieldGroup)
    {
        if (fieldGroup.Length == 0)
            return [(null!, await SumScalar(fieldNumeric))];

        var allFieldIds = fieldGroup.Select(f => f.ID).Append(fieldNumeric.ID).Distinct().ToList();
        var inClause = string.Join(", ", allFieldIds);

        var pivotCols = string.Join(",\n                ",
            allFieldIds.Select(id =>
                $"MAX(CASE WHEN v.\"FieldID\" = {id} THEN v.\"ValueStringFormatted\" END) AS \"Value_{id}\""));

        var groupColAliases = fieldGroup.Select(f => $"\"Value_{f.ID}\"").ToList();
        var groupByClause = string.Join(", ", groupColAliases);
        var keyExpr = groupColAliases.Count == 1
            ? groupColAliases[0]
            : "CONCAT(" + string.Join(", '|', ", groupColAliases) + ")";

        var sql = $"""
            SELECT
                {keyExpr} AS "Key",
                COALESCE(SUM(CAST("Value_{fieldNumeric.ID}" AS DOUBLE PRECISION)), 0) AS "Sum"
            FROM (
                SELECT
                    v."RowIndex",
                    {pivotCols}
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({inClause})
                GROUP BY v."RowIndex"
            ) AS pivoted
            WHERE "Value_{fieldNumeric.ID}" IS NULL OR "Value_{fieldNumeric.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
            GROUP BY {groupByClause}
            """;

        var rows = await _lqDbContext.Database.SqlQueryRaw<SumRow>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.Key ?? ""), r.Sum)).ToList();
    }

    public override async Task<(object Key, double Sum)> Sum(Field fieldNumeric)
        => (null!, await SumScalar(fieldNumeric));

    private async Task<double> SumScalar(Field fieldNumeric)
    {
        var sql = $"""
            SELECT COALESCE(SUM(CAST(v."ValueStringFormatted" AS DOUBLE PRECISION)), 0) AS "Value"
            FROM "app"."Value" v
            WHERE v."FieldID" = {fieldNumeric.ID}
              AND v."ValueStringFormatted" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
            """;
        return await _lqDbContext.Database.SqlQueryRaw<double>(sql).FirstAsync();
    }

    private record SumRow
    {
        public string? Key { get; init; }
        public double Sum { get; init; }
    }

    // ─── Count aggregates ─────────────────────────────────────────────────────

    public override async Task<List<(object Key, int Count)>> CountByGroup(Field groupField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        string sql;
        if (IsListField(groupField))
        {
            sql = $"""
                SELECT TRIM(cat) AS "Key", COUNT(*)::int AS "Count"
                FROM "app"."Value" v,
                     LATERAL UNNEST(string_to_array(v."ValueStringFormatted", ',')) AS cat
                WHERE v."FieldID" = {groupField.ID}
                  AND v."ValueStringFormatted" IS NOT NULL
                  {rf}
                GROUP BY TRIM(cat)
                """;
        }
        else
        {
            sql = $"""
                SELECT v."ValueStringFormatted" AS "Key", COUNT(*)::int AS "Count"
                FROM "app"."Value" v
                WHERE v."FieldID" = {groupField.ID}
                  AND v."ValueStringFormatted" IS NOT NULL
                  {rf}
                GROUP BY v."ValueStringFormatted"
                """;
        }
        var rows = await _lqDbContext.Database.SqlQueryRaw<CountRow>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.Key ?? ""), r.Count)).ToList();
    }

    public override async Task<List<(object KeyA, object KeyB, int Count)>> CountByGroup(Field groupField1, Field groupField2, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        var (keyA, latA) = ListExpr(groupField1, "cat_a");
        var (keyB, latB) = ListExpr(groupField2, "cat_b");
        var sql = $"""
            SELECT
                {keyA} AS "KeyA",
                {keyB} AS "KeyB",
                COUNT(*)::int AS "Count"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {groupField1.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupField1.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupField2.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupField2.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({groupField1.ID}, {groupField2.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{latA}{latB}
            GROUP BY {keyA}, {keyB}
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<Count2Row>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.KeyA ?? ""), (object)(r.KeyB ?? ""), r.Count)).ToList();
    }

    public override async Task<List<(object Key, int CountDistinct)>> CountDistinctByGroup(Field distinctField, Field groupField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        var (keyExpr, lateral) = ListExpr(groupField, "grp_cat");
        var sql = $"""
            SELECT
                {keyExpr} AS "Key",
                COUNT(DISTINCT "Value_{distinctField.ID}")::int AS "CountDistinct"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {distinctField.ID} THEN v."ValueStringFormatted" END) AS "Value_{distinctField.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupField.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({distinctField.ID}, {groupField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{lateral}
            GROUP BY {keyExpr}
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<CountDistinctRow>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.Key ?? ""), r.CountDistinct)).ToList();
    }

    // ─── Average aggregates ───────────────────────────────────────────────────

    public override async Task<List<(object Key, double Average)>> AverageByGroup(Field numericField, Field groupField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        var (keyExpr, lateral) = ListExpr(groupField, "grp_cat");
        var sql = $"""
            SELECT
                {keyExpr} AS "Key",
                AVG(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Average"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupField.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {groupField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{lateral}
            WHERE "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
            GROUP BY {keyExpr}
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<AvgRow>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.Key ?? ""), r.Average)).ToList();
    }

    public override async Task<List<(object KeyA, object KeyB, double Average)>> AverageByGroup(Field numericField, Field groupFieldA, Field groupFieldB, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        var (keyA, latA) = ListExpr(groupFieldA, "cat_a");
        var (keyB, latB) = ListExpr(groupFieldB, "cat_b");
        var sql = $"""
            SELECT
                {keyA} AS "KeyA",
                {keyB} AS "KeyB",
                AVG(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Average"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupFieldA.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupFieldA.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupFieldB.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupFieldB.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {groupFieldA.ID}, {groupFieldB.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{latA}{latB}
            WHERE "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
            GROUP BY {keyA}, {keyB}
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<Avg2Row>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.KeyA ?? ""), (object)(r.KeyB ?? ""), r.Average)).ToList();
    }

    // ─── Five-number summary ──────────────────────────────────────────────────

    public override async Task<List<(object Key, double Min, double Q1, double Median, double Q3, double Max)>> FiveNumberSummaryByGroup(Field numericField, Field groupField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return [];
        var rf = RF(rowFilter);
        var (keyExpr, lateral) = ListExpr(groupField, "grp_cat");
        var sql = $"""
            SELECT
                {keyExpr} AS "Key",
                MIN(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Min",
                PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Q1",
                PERCENTILE_CONT(0.5)  WITHIN GROUP (ORDER BY CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Median",
                PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Q3",
                MAX(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Max"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {groupField.ID} THEN v."ValueStringFormatted" END) AS "Value_{groupField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {groupField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{lateral}
            WHERE "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
            GROUP BY {keyExpr}
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<FiveNumRow>(sql).ToListAsync();
        return rows.Select(r => ((object)(r.Key ?? ""), r.Min, r.Q1, r.Median, r.Q3, r.Max)).ToList();
    }

    // ─── Scalar min/max ───────────────────────────────────────────────────────

    public override async Task<(double Min, double Max)> MinMax(Field numericField, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return (0, 0);
        var rf = RF(rowFilter);
        var sql = $"""
            SELECT
                MIN(CAST(v."ValueStringFormatted" AS DOUBLE PRECISION)) AS "Min",
                MAX(CAST(v."ValueStringFormatted" AS DOUBLE PRECISION)) AS "Max"
            FROM "app"."Value" v
            WHERE v."FieldID" = {numericField.ID}
              AND v."ValueStringFormatted" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$'
              {rf}
            """;
        var row = await _lqDbContext.Database.SqlQueryRaw<MinMaxRow>(sql).FirstAsync();
        return (row.Min, row.Max);
    }

    // ─── Time-bucketed aggregates ─────────────────────────────────────────────

    public override async Task<SortedDictionary<DateTime, int>> CountByTimeBucket(Field dateField, TimeGranularity granularity, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<DateTime, int>();
        var gran = ToPostgresGranularity(granularity);
        var rf = RF(rowFilter);
        var dt = ParseDT(@"v.""ValueStringFormatted""");
        var sql = $"""
            SELECT DATE_TRUNC('{gran}', {dt}) AS "Bucket",
                   COUNT(*)::int AS "Count"
            FROM "app"."Value" v
            WHERE v."FieldID" = {dateField.ID}
              AND v."ValueStringFormatted" IS NOT NULL
              AND v."ValueStringFormatted" != ''
              {rf}
            GROUP BY 1
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<TimeBucketCountRow>(sql).ToListAsync();
        var result = new SortedDictionary<DateTime, int>();
        foreach (var r in rows) result[DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc)] = r.Count;
        return result;
    }

    public override async Task<SortedDictionary<DateTime, double>> AverageByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<DateTime, double>();
        var gran = ToPostgresGranularity(granularity);
        var rf = RF(rowFilter);
        var dt = ParseDT($@"""Value_{dateField.ID}""");
        var sql = $"""
            SELECT DATE_TRUNC('{gran}', {dt}) AS "Bucket",
                   AVG(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Average"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {dateField.ID} THEN v."ValueStringFormatted" END) AS "Value_{dateField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {dateField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted
            WHERE "Value_{dateField.ID}" IS NOT NULL AND "Value_{dateField.ID}" != ''
              AND ("Value_{numericField.ID}" IS NULL OR "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$')
            GROUP BY 1
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<TimeBucketAvgRow>(sql).ToListAsync();
        var result = new SortedDictionary<DateTime, double>();
        foreach (var r in rows) result[DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc)] = r.Average;
        return result;
    }

    public override async Task<SortedDictionary<DateTime, double>> SumByTimeBucket(Field numericField, Field dateField, TimeGranularity granularity, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<DateTime, double>();
        var gran = ToPostgresGranularity(granularity);
        var rf = RF(rowFilter);
        var dt = ParseDT($@"""Value_{dateField.ID}""");
        var sql = $"""
            SELECT DATE_TRUNC('{gran}', {dt}) AS "Bucket",
                   COALESCE(SUM(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)), 0) AS "Sum"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {dateField.ID} THEN v."ValueStringFormatted" END) AS "Value_{dateField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {dateField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted
            WHERE "Value_{dateField.ID}" IS NOT NULL AND "Value_{dateField.ID}" != ''
              AND ("Value_{numericField.ID}" IS NULL OR "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$')
            GROUP BY 1
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<TimeBucketSumRow>(sql).ToListAsync();
        var result = new SortedDictionary<DateTime, double>();
        foreach (var r in rows) result[DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc)] = r.Sum;
        return result;
    }

    public override async Task<SortedDictionary<DateTime, Dictionary<string, int>>> CountByTimeBucketAndCategory(Field dateField, Field catField, TimeGranularity granularity, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<DateTime, Dictionary<string, int>>();
        var gran = ToPostgresGranularity(granularity);
        var rf = RF(rowFilter);
        var (catExpr, lateral) = ListExpr(catField, "cat_item");
        var dt = ParseDT($@"""Value_{dateField.ID}""");
        var sql = $"""
            SELECT DATE_TRUNC('{gran}', {dt}) AS "Bucket",
                   {catExpr} AS "Category",
                   COUNT(*)::int AS "Count"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {dateField.ID} THEN v."ValueStringFormatted" END) AS "Value_{dateField.ID}",
                    MAX(CASE WHEN v."FieldID" = {catField.ID} THEN v."ValueStringFormatted" END) AS "Value_{catField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({dateField.ID}, {catField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{lateral}
            WHERE "Value_{dateField.ID}" IS NOT NULL AND "Value_{dateField.ID}" != ''
            GROUP BY 1, 2
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<TimeBucketCatCountRow>(sql).ToListAsync();
        var result = new SortedDictionary<DateTime, Dictionary<string, int>>();
        foreach (var r in rows)
        {
            var bucket = DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc);
            if (!result.TryGetValue(bucket, out var dict)) result[bucket] = dict = new Dictionary<string, int>();
            dict[r.Category ?? ""] = r.Count;
        }
        return result;
    }

    public override async Task<SortedDictionary<DateTime, Dictionary<string, double>>> AverageByTimeBucketAndCategory(Field numericField, Field dateField, Field catField, TimeGranularity granularity, int[]? rowFilter = null)
    {
        if (rowFilter is { Length: 0 }) return new SortedDictionary<DateTime, Dictionary<string, double>>();
        var gran = ToPostgresGranularity(granularity);
        var rf = RF(rowFilter);
        var (catExpr, lateral) = ListExpr(catField, "cat_item");
        var dt = ParseDT($@"""Value_{dateField.ID}""");
        var sql = $"""
            SELECT DATE_TRUNC('{gran}', {dt}) AS "Bucket",
                   {catExpr} AS "Category",
                   AVG(CAST("Value_{numericField.ID}" AS DOUBLE PRECISION)) AS "Average"
            FROM (
                SELECT
                    v."RowIndex",
                    MAX(CASE WHEN v."FieldID" = {numericField.ID} THEN v."ValueStringFormatted" END) AS "Value_{numericField.ID}",
                    MAX(CASE WHEN v."FieldID" = {dateField.ID} THEN v."ValueStringFormatted" END) AS "Value_{dateField.ID}",
                    MAX(CASE WHEN v."FieldID" = {catField.ID} THEN v."ValueStringFormatted" END) AS "Value_{catField.ID}"
                FROM "app"."Value" v
                WHERE v."FieldID" IN ({numericField.ID}, {dateField.ID}, {catField.ID})
                  {rf}
                GROUP BY v."RowIndex"
            ) AS pivoted{lateral}
            WHERE "Value_{dateField.ID}" IS NOT NULL AND "Value_{dateField.ID}" != ''
              AND ("Value_{numericField.ID}" IS NULL OR "Value_{numericField.ID}" ~ '^-?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?$')
            GROUP BY 1, 2
            ORDER BY 1
            """;
        var rows = await _lqDbContext.Database.SqlQueryRaw<TimeBucketCatAvgRow>(sql).ToListAsync();
        var result = new SortedDictionary<DateTime, Dictionary<string, double>>();
        foreach (var r in rows)
        {
            var bucket = DateTime.SpecifyKind(r.Bucket, DateTimeKind.Utc);
            if (!result.TryGetValue(bucket, out var dict)) result[bucket] = dict = new Dictionary<string, double>();
            dict[r.Category ?? ""] = r.Average;
        }
        return result;
    }

    // ─── SQL helpers ──────────────────────────────────────────────────────────

    private static string ToPostgresGranularity(TimeGranularity g) => g switch
    {
        TimeGranularity.Hour    => "hour",
        TimeGranularity.Day     => "day",
        TimeGranularity.Week    => "week",
        TimeGranularity.Month   => "month",
        TimeGranularity.Quarter => "quarter",
        TimeGranularity.Year    => "year",
        _                       => "day"
    };

    // ─── SqlQueryRaw DTOs ─────────────────────────────────────────────────────

    private record CountRow          { public string? Key { get; init; }  public int Count { get; init; } }
    private record Count2Row         { public string? KeyA { get; init; } public string? KeyB { get; init; } public int Count { get; init; } }
    private record CountDistinctRow  { public string? Key { get; init; }  public int CountDistinct { get; init; } }
    private record AvgRow            { public string? Key { get; init; }  public double Average { get; init; } }
    private record Avg2Row           { public string? KeyA { get; init; } public string? KeyB { get; init; } public double Average { get; init; } }
    private record FiveNumRow        { public string? Key { get; init; }  public double Min { get; init; } public double Q1 { get; init; } public double Median { get; init; } public double Q3 { get; init; } public double Max { get; init; } }
    private record MinMaxRow         { public double Min { get; init; }   public double Max { get; init; } }
    private record TimeBucketCountRow   { public DateTime Bucket { get; init; } public int Count { get; init; } }
    private record TimeBucketAvgRow     { public DateTime Bucket { get; init; } public double Average { get; init; } }
    private record TimeBucketSumRow     { public DateTime Bucket { get; init; } public double Sum { get; init; } }
    private record TimeBucketCatCountRow { public DateTime Bucket { get; init; } public string? Category { get; init; } public int Count { get; init; } }
    private record TimeBucketCatAvgRow  { public DateTime Bucket { get; init; } public string? Category { get; init; } public double Average { get; init; } }

    // ─── Canvas persistence ───────────────────────────────────────────────────

    public override async Task<string?> GetCanvasDocument(int csvFileId)
    {
        return await _lqDbContext.Canvases
            .AsNoTracking()
            .Where(c => c.CSVFileID == csvFileId)
            .Select(c => c.Document)
            .SingleOrDefaultAsync();
    }

    public override async Task UpsertCanvasDocument(int csvFileId, string documentJson)
    {
        var existing = await _lqDbContext.Canvases.FindAsync(csvFileId);
        if (existing is null)
        {
            _lqDbContext.Canvases.Add(new Canvas(csvFileId, documentJson));
        }
        else
        {
            existing.Document = documentJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _lqDbContext.SaveChangesAsync();
    }
}
