using EFCore.BulkExtensions;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;

namespace LQ.Services.Implementations;

public class LQDbService : ILQDbService
{
    private readonly LQDbContext _lqdbContext;

    public LQDbService(LQDbContext lqdbContext)
    {
        _lqdbContext = lqdbContext;
    }
    
    public bool InsertAnalyst(Analyst analyst)
    {
        if (GetAnalyst(analyst.Name) is not null) return false;
        _lqdbContext.Add(analyst);
        _lqdbContext.SaveChanges();
        return true;
    }
    public Analyst? GetAnalyst(string name)
    {
        return (from a in _lqdbContext.Analysts where a.Name == name select a).FirstOrDefault();
    }
    public Analyst? GetAnalyst(int id)
    {
        return (from a in _lqdbContext.Analysts where a.ID == id select a).FirstOrDefault();
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

    public Table? GetTable(int analystid, string filename)
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

    public Table? GetTable(int analystid, int csvfileid)
    {
        CSVFile? csvFile = (from f in _lqdbContext.CSVFiles 
            where f.ID == csvfileid && f.Analyst.ID == analystid 
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
    
    public void InsertCSVFile(CSVFile csvFile)
    {
        _lqdbContext.CSVFiles.Add(csvFile);
        _lqdbContext.SaveChanges();
    }
    public CSVFile? GetCSVFile(Analyst analyst, string filename)
    {
        return GetCSVFile(analyst.ID, filename);
    }

    public CSVFile? GetCSVFile(int analystid, string filename)
    {
        return (from f in _lqdbContext.CSVFiles
                where f.Analyst.ID == analystid && f.Name == filename 
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
        return GetCSVFiles(analyst.ID);
    }

    public List<CSVFile> GetCSVFiles(int analystid)
    {
        return (from f in _lqdbContext.CSVFiles
                where f.Analyst.ID == analystid
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
            where a.Analyst.ID == analyst.ID && a.Name == fileName select a).Any();
    }
}