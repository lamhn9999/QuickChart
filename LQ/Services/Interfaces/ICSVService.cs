using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
namespace LQ.Services.Interfaces;

public interface ICSVService
{
    public bool Validate(Analyst analyst, string path);
    public bool Validate(Analyst analyst, IFormFile file);
    public Task<bool> ProcessUploadedCSV(Analyst analyst, string path);
    public Task<bool> ProcessUploadedCSV(Analyst analyst, IFormFile file);
    public Table? GetTable(int analystId, string filename);
    public Table? GetTable(int analystId, int csvFileId);
    public bool DetectDateTime(FieldValues fieldValues);
}