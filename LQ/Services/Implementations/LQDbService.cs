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

    public void InsertRangeValue(List<Value> values)
    {
        _lqdbContext.Values.AddRange(values);
        _lqdbContext.SaveChanges();
    }

    public void InsertCSVFile(CSVFile csvFile)
    {
        _lqdbContext.CSVFiles.Add(csvFile);
        _lqdbContext.SaveChanges();
    }

    public CSVFile? GetCSVFile(Analyst analyst, string filename)
    {
        return (from f in _lqdbContext.CSVFiles
                where f.AnalystID == analyst.ID && f.Name == filename 
                select f)
            .FirstOrDefault();
    }
    public void InsertTable(Table table)
    {
        InsertCSVFile(table.CSVFile);
        table.Show(25);
        foreach (FieldValues fieldvalues in table.Columns)
        {
            InsertField(fieldvalues.Field);
            InsertRangeValue(fieldvalues.Values);
        }
        _lqdbContext.SaveChanges();
    }

    public Table GetTable(CSVFile csvFile)
    {
        Table returnTable = new Table();
        returnTable.CSVFile = csvFile;
        List<Field> fields = (from f in _lqdbContext.Fields where f.CSVFile == csvFile select f).ToList();
        foreach (Field f in fields)
        {
            List<Value> values = (from v in _lqdbContext.Values where v.Field == f select v).ToList();
            returnTable.Columns.Add(new FieldValues{Field = f,  Values = values});
        }

        return returnTable;
    }

    public void InsertAnalyst(Analyst analyst)
    {
        _lqdbContext.Add(analyst);
        _lqdbContext.SaveChanges();
    }

    public List<Analyst> GetAnalyst(string name)
    {
        return (from a in _lqdbContext.Analysts where a.Name == name select a).ToList();
    }

    public Analyst? GetAnalyst(int id)
    {
        return (from a in _lqdbContext.Analysts where a.ID == id select a).FirstOrDefault();
    }
}