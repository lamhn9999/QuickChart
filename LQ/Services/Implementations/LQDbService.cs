using EFCore.BulkExtensions;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using LQ.Models.DTOs;
using Microsoft.EntityFrameworkCore;

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
    
    public Table? GetTable(CSVFile csvFile)
    {
        Table returnTable = new Table(csvFile);
        List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile orderby f.Index select f).ToList();
        foreach (Field f in fields)
        {
            List<Value> values = (from v in _lqdbContext.Values orderby v.RowIndex where v.Field.ID == f.ID select v).ToList();
            returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
        }
        returnTable.SetRowCount();
        return returnTable;
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
        return GetTable(GetCSVFile(csvfileid));
    }

    public Table? GetTable(string analystid, int csvfileid)
    {
        CSVFile? csvFile = (from f in _lqdbContext.CSVFiles 
            where f.ID == csvfileid && f.AnalystID == analystid 
            select f).FirstOrDefault();
        return csvFile is null
            ? null
            : GetTable(csvFile);
    }

    public Table GetTable(int csvfileid, List<int> fieldindexes)
    {
        Table returnTable = new Table(GetCSVFile(csvfileid));
        List<Field> fields = (from f in _lqdbContext.Fields 
            where f.CSVFile.ID == csvfileid && (fieldindexes.Contains(f.Index))
            // where f.CSVFile.ID == csvfileid && (fieldindexes.BinarySearch(f.Index) >= 0)
            select f).ToList();
        foreach (Field f in fields)
        {
            List<Value> values = (from v in _lqdbContext.Values orderby v.RowIndex where v.Field.ID == f.ID select v).ToList();
            returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
        }
        returnTable.SetRowCount();
        return returnTable;
    }

    public Table? GetTableLimited(int csvfileid, int numRowLimited)
    {
        try
        {
            Table returnTable;
            CSVFile? csvFile = GetCSVFile(csvfileid);
            returnTable = new Table(GetCSVFile(csvfileid));
            List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile orderby f.Index select f).ToList();
            foreach (Field f in fields)
            {
                List<Value> values = (from v in _lqdbContext.Values orderby v.RowIndex where v.Field.ID == f.ID select v).Take(numRowLimited).ToList();
                returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
            }
            returnTable.SetRowCount();
            return returnTable;
        }
        catch 
        {
            return null;
        }
    }

    private IQueryable<int> GetFilteredRowIndexesQuery(int csvfileid, List<ValueFilterDTO> filters)
    {
        if (filters.Count == 0)
        {
            return _lqdbContext.Values.Where(v => v.Field.CSVFile.ID == csvfileid).Select(v => v.RowIndex).Distinct();
        }

        IQueryable<int>? finalQuery = null;

        foreach (var filter in filters)
        {
            var filterQuery = _lqdbContext.Values.Where(v => v.Field.CSVFile.ID == csvfileid && v.Field.Index == filter.FieldIndex);

            if (filter.IsPartialMatch)
            {
                filterQuery = filterQuery.Where(v =>
                    filter.FilteringValues.Any(s => EF.Functions.Like(v.ValueString, s)));
            }
            else
            {
                filterQuery = filterQuery.Where(v =>
                    filter.FilteringValues.Contains(v.ValueString));
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

    public Table? GetTableWithFilter(int csvfileid, List<ValueFilterDTO> filters)
    {
        try 
        {
            CSVFile? csvFile = GetCSVFile(csvfileid);
            if (csvFile == null) return null;

            Table returnTable = new Table(csvFile);
            List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile orderby f.Index select f).ToList();
            
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            
            foreach (Field f in fields)
            {
                List<Value> values = (from v in _lqdbContext.Values 
                                      where v.Field.ID == f.ID && validRowIndexesQuery.Contains(v.RowIndex) 
                                      orderby v.RowIndex 
                                      select v).ToList();
                returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
            }
            returnTable.SetRowCount();
            return returnTable;
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

            Table returnTable = new Table(csvFile);
            List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile && fieldindexes.Contains(f.Index) orderby f.Index select f).ToList();
            
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            
            foreach (Field f in fields)
            {
                List<Value> values = (from v in _lqdbContext.Values 
                    where v.Field.ID == f.ID && validRowIndexesQuery.Contains(v.RowIndex) 
                    orderby v.RowIndex 
                    select v).ToList();
                returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
            }
            returnTable.SetRowCount();
            return returnTable;
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

            Table returnTable = new Table(csvFile);
            List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile orderby f.Index select f).ToList();
            
            var validRowIndexesQuery = GetFilteredRowIndexesQuery(csvfileid, filters);
            var limitedRowIndexesList = validRowIndexesQuery.OrderBy(r => r).Take(numRowLimited).ToList();
            
            foreach (Field f in fields)
            {
                List<Value> values = (from v in _lqdbContext.Values 
                                      where v.Field.ID == f.ID && limitedRowIndexesList.Contains(v.RowIndex) 
                                      orderby v.RowIndex 
                                      select v).ToList();
                returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
            }
            returnTable.SetRowCount();
            return returnTable;
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
        return (from f in _lqdbContext.CSVFiles
                where f.AnalystID == analystid && f.Name == filename 
                select f)
            .FirstOrDefault();
    }

    public CSVFile? GetCSVFile(int csvfileid)
    {
        return (from f in _lqdbContext.CSVFiles
            where f.ID == csvfileid
            select f).FirstOrDefault();
    }

    public List<CSVFile> GetCSVFiles(Analyst analyst)
    {
        return GetCSVFiles(analyst.Id);
    }

    public List<CSVFile> GetCSVFiles(string analystid)
    {
        return (from f in _lqdbContext.CSVFiles
                where f.AnalystID == analystid
                select f)
            .ToList();
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


    public bool SearchFileByAnalyst(Analyst analyst, string fileName)
    {
        return (from a in _lqdbContext.CSVFiles 
            where a.AnalystID == analyst.Id && a.Name == fileName select a).Any();
    }

    public void RemoveCSVFile(int CSVFileID)
    {
        _lqdbContext.CSVFiles.Where(f => f.ID == CSVFileID).
            ExecuteDeleteAsync();
        _lqdbContext.SaveChanges();
    }

    public void RenameCSVFile(int CSVFileID, string newName)
    {
        _lqdbContext.CSVFiles.Where(f => f.ID == CSVFileID).
            ExecuteUpdateAsync(setters => setters.SetProperty(f => f.Name, newName));
        _lqdbContext.SaveChanges();
    }
}