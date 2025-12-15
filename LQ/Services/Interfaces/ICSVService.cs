using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
namespace LQ.Services.Interfaces;

public interface ICSVService
{
    public bool Validate(Analyst analyst, string path);
    public bool Validate(Analyst analyst, IFormFile file);
    public bool ProcessUploadedCSV(Analyst analyst, string path);
    public bool ProcessUploadedCSV(Analyst analyst, IFormFile file);
    public Table? GetTable(int AnalystID, string filename);
}