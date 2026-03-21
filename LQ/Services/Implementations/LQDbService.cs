using System.Diagnostics;
using EFCore.BulkExtensions;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using LQ.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LQ.Services.Implementations;

public class LQDbService : ILQDbService
{
    private readonly LQDbContext _lqdbContext;
    private readonly ILQIdentityDbService _lqIdentityDbService;

    public LQDbService(LQDbContext lqdbContext, ILQIdentityDbService lqIdentityDbService)
    {
        _lqdbContext = lqdbContext;
        _lqIdentityDbService = lqIdentityDbService;
    }
    
    public void InsertTable(Table table)
    {
        InsertCSVFile(table.CSVFile);
        foreach (FieldValues fieldvalues in table.Columns)
        {
            InsertField(fieldvalues.Field);
            InsertValues(fieldvalues.Values);
        }
    }

    public async Task InsertTableAsync(Table table)
    {
        InsertCSVFile(table.CSVFile);
        foreach (FieldValues fieldvalues in table.Columns)
        {
            InsertField(fieldvalues.Field);
        }
        await _lqdbContext.SaveChangesAsync();
        List<Field> addedFields = (from F in _lqdbContext.Fields where F.CSVFile == table.CSVFile orderby F.Index select F).ToList();
        for (int i = 0; i < addedFields.Count; i++)
        {
            foreach (Value V in table.Columns[i].Values)
            {
                V.FieldID = addedFields[i].ID;
            }

            await InsertValuesAsync(table.Columns[i].Values);
        }
    }

    private Table BuildTableFromFieldsAndValues(CSVFile csvFile, List<Field> fields, List<Value> allValues)
    {
        Table returnTable = new Table(csvFile);
        var valuesByFieldId = allValues.GroupBy(v => v.FieldID).ToDictionary(g => g.Key, g => g.ToList());

        foreach (Field f in fields)
        {
            returnTable.Columns.Add(new FieldValues{Field = f, Values = valuesByFieldId.GetValueOrDefault(f.ID, new List<Value>())});
        }
        returnTable.SetRowCount();
        return returnTable;
    }

    private IQueryable<Field> GetFieldsQuery(int csvfileid, List<int>? fieldindexes = null)
    {
        if (fieldindexes != null)
        {
            return _lqdbContext.Fields
                .Where(f => f.CSVFile.ID == csvfileid && fieldindexes.Contains(f.Index))
                .OrderBy(f => f.Index);
        }
        return _lqdbContext.Fields
            .Where(f => f.CSVFile.ID == csvfileid)
            .OrderBy(f => f.Index);
    }

    private async Task<List<Field>> GetFieldsAsync(int csvfileid, List<int>? fieldindexes = null)
    {
        if (fieldindexes != null)
        {
            return await (from f in _lqdbContext.Fields 
                          where f.CSVFile.ID == csvfileid && fieldindexes.Contains(f.Index) 
                          orderby f.Index 
                          select f).ToListAsync();
        }
        return await (from f in _lqdbContext.Fields 
                      where f.CSVFile.ID == csvfileid 
                      orderby f.Index 
                      select f).ToListAsync();
    }
    
    public async Task<Table> GetTableAsync(int csvfileid)
    {
        CSVFile csvFile = GetCSVFile(csvfileid)!;
        List<Field> fields = await GetFieldsAsync(csvfileid);
        List<int> fieldIds = fields.Select(f => f.ID).ToList();
        
        List<Value> allValues = await (from v in _lqdbContext.Values 
                                       where fieldIds.Contains(v.FieldID) 
                                       orderby v.RowIndex 
                                       select v).ToListAsync();
                                       
        return BuildTableFromFieldsAndValues(csvFile, fields, allValues);
    }

    public async Task<Table> GetTableAsync(int csvfileid, List<int> fieldindexes)
    {
        CSVFile csvFile = GetCSVFile(csvfileid)!;
        List<Field> fields = await GetFieldsAsync(csvfileid, fieldindexes);
        var fieldIds = fields.Select(f => f.ID).ToList();
        
        var allValues = await (from v in _lqdbContext.Values 
                               where fieldIds.Contains(v.FieldID) 
                               orderby v.RowIndex 
                               select v).ToListAsync();
                               
        return BuildTableFromFieldsAndValues(csvFile, fields, allValues);
    }

    public Table? GetTable(CSVFile csvFile)
    {
        var fieldsQuery = GetFieldsQuery(csvFile.ID);
        var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
        
        var fields = fieldsQuery.ToList();
        var allValues = _lqdbContext.Values
            .Where(v => fieldIdsQuery.Contains(v.FieldID))
            .OrderBy(v => v.RowIndex)
            .ToList();
                         
        return BuildTableFromFieldsAndValues(csvFile, fields, allValues);
    }

    public Table? GetTable(string analystid, string filename)
    {
        CSVFile? csvfile = GetCSVFile(analystid, filename);
        return csvfile is null
            ? null
            : GetTable(csvfile);
    }

    public Table? GetTable(int csvfileid)
    {
        return GetTable(GetCSVFile(csvfileid)!);
    }

    public Table? GetTable(string analystid, int csvfileid)
    {
        CSVFile? csvFile = _lqdbContext.CSVFiles.FirstOrDefault(f => f.ID == csvfileid && f.AnalystID == analystid);
        return csvFile is null
            ? null
            : GetTable(csvFile);
    }

    public Table? GetTable(int csvfileid, List<int> fieldindexes)
    {
        CSVFile? csvFile = GetCSVFile(csvfileid);
        if (csvFile == null) return null;
        
        var fieldsQuery = GetFieldsQuery(csvfileid, fieldindexes);
        var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
        
        var fields = fieldsQuery.ToList();
        var allValues = _lqdbContext.Values
            .Where(v => fieldIdsQuery.Contains(v.FieldID))
            .OrderBy(v => v.RowIndex)
            .ToList();
                         
        return BuildTableFromFieldsAndValues(csvFile, fields, allValues);
    }

    public Table? GetTableLimited(int csvfileid, int numRowLimited)
    {
        try
        {
            CSVFile? csvFile = GetCSVFile(csvfileid);
            if (csvFile == null) return null;
            
            var fieldsQuery = GetFieldsQuery(csvfileid);
            var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
            
            var limitedRowIndexesQuery = _lqdbContext.Values
                .Where(v => fieldIdsQuery.Contains(v.FieldID))
                .Select(v => v.RowIndex)
                .Distinct()
                .OrderBy(r => r)
                .Take(numRowLimited);
                                     
            var allValues = _lqdbContext.Values
                .Where(v => fieldIdsQuery.Contains(v.FieldID) && limitedRowIndexesQuery.Contains(v.RowIndex))
                .OrderBy(v => v.RowIndex)
                .ToList();
            
            return BuildTableFromFieldsAndValues(csvFile, fieldsQuery.ToList(), allValues);
        }
        catch 
        {
            return null;
        }
    }

    private IQueryable<int> GetFilteredRowIndexesQuery(int csvfileid, List<ValueFilterDTO> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return _lqdbContext.Values.Where(v => v.Field.CSVFile.ID == csvfileid).Select(v => v.RowIndex).Distinct();
        }

        IQueryable<int>? finalQuery = null;

        foreach (var filter in filters)
        {
            var filterQuery = _lqdbContext.Values.Where(v => v.Field.CSVFile.ID == csvfileid && v.Field.Index == filter.FieldIndex);
   
            switch (filter.FieldType)
            {
                case FilterFieldType.Int:
                    var listInt = filter.FilteringValuesInt;
                    var boundInt = filter.IntBound;
                    bool isNotInt = filter.IsNot;

                    filterQuery = filterQuery.Where(v => v.ValueStringFormatted != null && v.ValueStringFormatted != "");

                    if (listInt != null && listInt.Count > 0)
                    {
                        filterQuery = isNotInt 
                            ? filterQuery.Where(v => !listInt.Contains(Convert.ToInt32(v.ValueStringFormatted)))
                            : filterQuery.Where(v => listInt.Contains(Convert.ToInt32(v.ValueStringFormatted)));
                    }

                    if (boundInt != null)
                    {
                        filterQuery = isNotInt 
                            ? filterQuery.Where(v => !(Convert.ToInt32(v.ValueStringFormatted) >= boundInt.Value.leftBound && Convert.ToInt32(v.ValueStringFormatted) <= boundInt.Value.rightBound))
                            : filterQuery.Where(v => Convert.ToInt32(v.ValueStringFormatted) >= boundInt.Value.leftBound && Convert.ToInt32(v.ValueStringFormatted) <= boundInt.Value.rightBound);
                    }
                    break;

                case FilterFieldType.Double:
                    var listDouble = filter.FilteringValuesDouble;
                    var boundDouble = filter.DoubleBound;
                    bool isNotDouble = filter.IsNot;

                    filterQuery = filterQuery.Where(v => v.ValueStringFormatted != null && v.ValueStringFormatted != "");

                    if (listDouble != null && listDouble.Count > 0)
                    {
                        filterQuery = isNotDouble 
                            ? filterQuery.Where(v => !listDouble.Contains(Convert.ToDouble(v.ValueStringFormatted)))
                            : filterQuery.Where(v => listDouble.Contains(Convert.ToDouble(v.ValueStringFormatted)));
                    }

                    if (boundDouble != null)
                    {
                        filterQuery = isNotDouble 
                            ? filterQuery.Where(v => !(Convert.ToDouble(v.ValueStringFormatted) >= boundDouble.Value.leftBound && Convert.ToDouble(v.ValueStringFormatted) <= boundDouble.Value.rightBound))
                            : filterQuery.Where(v => Convert.ToDouble(v.ValueStringFormatted) >= boundDouble.Value.leftBound && Convert.ToDouble(v.ValueStringFormatted) <= boundDouble.Value.rightBound);
                    }
                    break;

                case FilterFieldType.DateTime:
                    if (filter.DateTimeBound != null)
                    {
                        var boundDt = filter.DateTimeBound.Value;
                        bool isNotDt = filter.IsNot;

                        var localDates = filterQuery.Select(v => new { v.RowIndex, v.ValueStringFormatted }).AsEnumerable();
                        var validDateRows = localDates.Where(v => {
                            if (string.IsNullOrEmpty(v.ValueStringFormatted)) return false;
                            if (DateTime.TryParse(v.ValueStringFormatted, out var dt)) {
                                bool inRange = dt >= boundDt.leftBound && dt <= boundDt.rightBound;
                                return isNotDt ? !inRange : inRange;
                            }
                            return false;
                        }).Select(v => v.RowIndex).ToList();

                        filterQuery = filterQuery.Where(v => validDateRows.Contains(v.RowIndex));
                    }
                    break;

                case FilterFieldType.ListString:
                    if (filter.FilteringValues != null && filter.FilteringValues.Count > 0)
                    {
                        filterQuery = filterQuery.Where(BuildStringExpression(filter.FilteringValues, filter.IsNot, filter.IsPartialMatch, true));
                    }
                    break;

                case FilterFieldType.Bool:
                    if (filter.FilteringValues != null && filter.FilteringValues.Count > 0)
                    {
                        filterQuery = filter.IsNot
                            ? filterQuery.Where(v => !filter.FilteringValues.Contains(v.ValueStringFormatted))
                            : filterQuery.Where(v => filter.FilteringValues.Contains(v.ValueStringFormatted));
                    }
                    break;
                    
                case FilterFieldType.String:
                    if (filter.FilteringValues != null && filter.FilteringValues.Count > 0)
                    {
                        filterQuery = filterQuery.Where(BuildStringExpression(filter.FilteringValues, filter.IsNot, filter.IsPartialMatch, false));
                    }
                    break;
            }

            var matchingRowIndexes = filterQuery.Select(v => v.RowIndex);

            if (finalQuery == null)
            {
                finalQuery = matchingRowIndexes;
            }
            else
            {
                finalQuery = finalQuery.Intersect(matchingRowIndexes);
            }
        }

        return finalQuery!;
    }

    private Expression<Func<Value, bool>> BuildStringExpression(List<string> list, bool isNot, bool isPartial, bool isListStr)
    {
        var vParam = Expression.Parameter(typeof(Value), "v");
        var property = Expression.Property(vParam, isListStr ? "ValueStringFormatted" : "ValueString");
        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        
        Expression? aggregate = null;
        foreach (var s in list)
        {
            Expression check;
            if (isPartial || isListStr) 
                check = Expression.Call(property, containsMethod, Expression.Constant(s));
            else 
                check = Expression.Equal(property, Expression.Constant(s));

            aggregate = aggregate == null 
                ? check 
                : (isPartial ? Expression.OrElse(aggregate, check) : Expression.AndAlso(aggregate, check));
        }

        if (aggregate == null) return v => false;

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        Expression combined = Expression.AndAlso(nullCheck, aggregate);
        if (isNot) combined = Expression.Not(combined);

        return Expression.Lambda<Func<Value, bool>>(combined, vParam);
    }

    public Table? GetTableWithFilter(int csvfileid, List<ValueFilterDTO> filters)
    {
        try 
        {
            CSVFile? csvFile = GetCSVFile(csvfileid);
            if (csvFile == null) return null;

            var fieldsQuery = GetFieldsQuery(csvfileid);
            var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            
            var allValues = _lqdbContext.Values
                .Where(v => fieldIdsQuery.Contains(v.FieldID) && validRowIndexesQuery.Contains(v.RowIndex))
                .OrderBy(v => v.RowIndex)
                .ToList();
                                     
            return BuildTableFromFieldsAndValues(csvFile, fieldsQuery.ToList(), allValues);
        }
        catch 
        {
            return null;
        }
    }

    public Table? GetTableWithFilter(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters)
    {
        try 
        {
            CSVFile? csvFile = GetCSVFile(csvfileid);
            if (csvFile == null) return null;

            var fieldsQuery = GetFieldsQuery(csvfileid, fieldindexes);
            var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            
            var allValues = _lqdbContext.Values
                .Where(v => fieldIdsQuery.Contains(v.FieldID) && validRowIndexesQuery.Contains(v.RowIndex))
                .OrderBy(v => v.RowIndex)
                .ToList();
                                     
            return BuildTableFromFieldsAndValues(csvFile, fieldsQuery.ToList(), allValues);
        }
        catch 
        {
            return null;
        }
    }

    public Table? GetTableWithFilterLimited(int csvfileid, List<ValueFilterDTO> filters, int numRowLimited)
    {
        try 
        {
            CSVFile? csvFile = GetCSVFile(csvfileid);
            if (csvFile == null) return null;

            var fieldsQuery = GetFieldsQuery(csvfileid);
            var fieldIdsQuery = fieldsQuery.Select(f => f.ID);
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            var limitedRowIndexesQuery = validRowIndexesQuery.OrderBy(r => r).Take(numRowLimited);
            
            var allValues = _lqdbContext.Values
                .Where(v => fieldIdsQuery.Contains(v.FieldID) && limitedRowIndexesQuery.Contains(v.RowIndex))
                .OrderBy(v => v.RowIndex)
                .ToList();
                             
            return BuildTableFromFieldsAndValues(csvFile, fieldsQuery.ToList(), allValues);
        }
        catch 
        {
            return null;
        }
    }

    public void InsertCSVFile(CSVFile csvFile)
    {
        _lqdbContext.CSVFiles.Add(csvFile);
        _lqdbContext.SaveChanges();
    }
    public CSVFile? GetCSVFile(Analyst analyst, string filename)
    {
        return GetCSVFile(analyst.Id, filename);
    }

    public CSVFile? GetCSVFile(string analystid, string filename)
    {
        return _lqdbContext.CSVFiles.FirstOrDefault(f => f.AnalystID == analystid && f.Name == filename);
    }

    public CSVFile? GetCSVFile(int csvfileid)
    {
        return _lqdbContext.CSVFiles.FirstOrDefault(f => f.ID == csvfileid);
    }

    public List<CSVFile> GetCSVFiles(Analyst analyst)
    {
        return GetCSVFiles(analyst.Id);
    }

    public List<CSVFile> GetCSVFiles(string analystid)
    {
        return _lqdbContext.CSVFiles.Where(f => f.AnalystID == analystid).ToList();
    }
    public void InsertField(Field field)
    {
        _lqdbContext.Fields.Add(field);
        _lqdbContext.SaveChanges();
    }
    public void InsertValue(Value value)
    {
        _lqdbContext.Values.Add(value);
        _lqdbContext.SaveChanges();
    }
    public void InsertValues(List<Value> values)
    {
        _lqdbContext.Values.AddRange(values);
        _lqdbContext.SaveChanges();
    }
    public async Task InsertValuesAsync(List<Value> values)
    {
        if (values.Count == 0) return;

        await _lqdbContext.BulkInsertAsync(values, new BulkConfig
        {
            BatchSize = 10_000,
            PreserveInsertOrder = true,
            SetOutputIdentity = true
        });
    }

    private async Task<ChangeFieldTypeDTO> TryChangeFieldType(Field targetField, Type newType)
    {
        var rawStrings = await _lqdbContext.Values
            .Where(v => v.Field == targetField && v.ValueStringFormatted != null)
            .OrderBy(v => v.RowIndex)
            .Select(v => v.ValueStringFormatted)
            .ToListAsync();

        var failedValues = rawStrings
            .Where(val => !CanConvert(val!, newType))
            .ToList();

        return new ChangeFieldTypeDTO(failedValues!);
    }

    private bool CanConvert(string value, Type targetType)
    {
        if (targetType == typeof(string)) return true;
        return targetType switch
        {
            { } t when t == typeof(int) => int.TryParse(value, out _),
            { } t when t == typeof(double) => double.TryParse(value, out _),
            { } t when t == typeof(DateTime) => DateTime.TryParse(value, out _),

            { } t when t == typeof(List<string>) => true,

            _ => false
        };
    }
    
    public async Task<bool> ChangeFieldType(int csvFileId, int fieldId, Type newType)
    {
        var target = await _lqdbContext.Fields
            .SingleOrDefaultAsync(f => f.ID == fieldId);
        if (target is null) return false;
        // Console.WriteLine($"Target: {target.TypeName}\nNew: {newType.AssemblyQualifiedName}");
        if (target.Type == newType) return true;
        target.Type = newType;
        await _lqdbContext.SaveChangesAsync();
        return true;
    }
    
    public bool SearchFileByAnalyst(Analyst analyst, string fileName)
    {
        return _lqdbContext.CSVFiles.Any(a => a.AnalystID == analyst.Id && a.Name == fileName);
    }

    public void RemoveCSVFile(int CSVFileID)
    {
        _lqdbContext.CSVFiles.Where(f => f.ID == CSVFileID).ExecuteDelete();
    }

    public void RenameCSVFile(int CSVFileID, string newName)
    {
        _lqdbContext.CSVFiles.Where(f => f.ID == CSVFileID).ExecuteUpdate(setters => setters.SetProperty(f => f.Name, newName));
    }
}