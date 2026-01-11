using LQ.Models.AnalystModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface ILQDbService
{
    public void InsertTable(Table table);
    public Task InsertTableAsync(Table table);
    public Table? GetTable(CSVFile csvFile);
    public Table? GetTable(string analystid, string filename);
    public Table? GetTable(int csvfileid);
    public Table? GetTable(string analystid, int csvfileid);
    public Table? GetTable(int csvfileid, List<int> fieldindexes);
    public void InsertCSVFile(CSVFile csvFile);
    public CSVFile? GetCSVFile(Analyst analyst, string filename);
    public CSVFile? GetCSVFile(string analystid, string filename);
    public CSVFile? GetCSVFile(int csvfileid);
    public List<CSVFile> GetCSVFiles(Analyst analyst);
    public List<CSVFile> GetCSVFiles(string analystid);
    public void InsertField(Field field);
    public void InsertValue(Value value);
    public void InsertValues(List<Value> values);
    public Task InsertValuesAsync(List<Value> values);
    public bool SearchFileByAnalyst(Analyst analyst,  string fileName);
}