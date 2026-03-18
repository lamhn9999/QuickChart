using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using LQ.Models.DTOs;

namespace LQ.Services.Interfaces;

public interface ILQDbService
{
    public void InsertTable(Table table);
    public Task InsertTableAsync(Table table);
    public Task<Table> GetTableAsync(int csvfileid);
    public Task<Table> GetTableAsync(int csvfileid, List<int> fieldindexes);
    public Table? GetTable(CSVFile csvFile);
    public Table? GetTable(string analystid, string filename);
    public Table? GetTable(int csvfileid);
    public Table? GetTable(string analystid, int csvfileid);
    public Table? GetTable(int csvfileid, List<int> fieldindexes);
    public Table? GetTableLimited(int csvfileid, int numRowLimited);
    public Table? GetTableWithFilter(int csvfileid, List<ValueFilterDTO> filters);
    public Table? GetTableWithFilter(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters);
    public Table? GetTableWithFilterLimited(int csvfileid, List<ValueFilterDTO> filters, int numRowLimited);
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
    public Task<bool> ChangeFieldType(int csvFileId, int fieldIndex, Type newType);
    public bool SearchFileByAnalyst(Analyst analyst,  string fileName);
    public void RemoveCSVFile(int CSVFileID);
    public void RenameCSVFile(int CSVFileID, string newName);
}