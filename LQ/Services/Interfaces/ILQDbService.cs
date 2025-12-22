using LQ.Models.AnalystModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface ILQDbService
{
    public bool InsertAnalyst(Analyst analyst);
    public Analyst? GetAnalyst(string name);
    public Analyst? GetAnalyst(int id);
    public void InsertTable(Table table);
    public Table? GetTable(CSVFile csvFile);
    public Table? GetTable(int analystid, string filename);
    public Table? GetTable(int csvfileid);
    public Table? GetTable(int analystid, int csvfileid);
    public Table? GetTable(int csvfileid, List<int> fieldindexes);
    public void InsertCSVFile(CSVFile csvFile);
    public CSVFile? GetCSVFile(Analyst analyst, string filename);
    public CSVFile? GetCSVFile(int analystid, string filename);
    public CSVFile? GetCSVFile(int csvfileid);
    public List<CSVFile> GetCSVFiles(Analyst analyst);
    public List<CSVFile> GetCSVFiles(int analystid);
    public void InsertField(Field field);
    public void InsertValue(Value value);
    public void InsertValues(List<Value> values);
    public bool SearchFileByAnalyst(Analyst analyst,  string fileName);
}