using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
namespace LQ.Services.Interfaces;

public interface ICSVService
{
    public bool Validate(Analyst analyst, string path);
    public bool Validate(Analyst analyst, IFormFile file);
    public Task<bool> ProcessUploadedCSV(Analyst analyst, string path);
    public Task<bool> ProcessUploadedCSV(Analyst analyst, IFormFile file);
    public Table? GetTable(string analystId, string filename);
    public Table? GetTable(string analystId, int csvFileId);
    public Table? GetTableLimited(int csvFileId, int numRowLimited);
    public bool DetectDateTime(FieldValues fieldValues);
    public Task<bool> ChangeFieldType(int csvFileId, int fieldIndex, string newType);
    public CSVFile? GetCSVFile(string analystid, string filename);
    public void RemoveCSVFile(string analystId, string fileName);
    public void RenameCSVFile(string analystId, string fileName, string newFileName);
}