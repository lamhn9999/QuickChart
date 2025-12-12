using LQ.Models.AnalystModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface ILQDbService
{
    public void InsertTable(Table table);
    public Table GetTable(CSVFile csvFile);
    public void InsertCSVFile(CSVFile csvFile);
    public CSVFile? GetCSVFile(Analyst analyst, string filename);
    public void InsertField(Field field);
    public void InsertValue(Value value);
    public void InsertAnalyst(Analyst analyst);
    public List<Analyst> GetAnalyst(string name);
    public Analyst? GetAnalyst(int id);
}