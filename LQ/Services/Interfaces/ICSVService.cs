using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
namespace LQ.Services.Interfaces;

public interface ICSVService
{
    public Task<bool> Validate(Analyst analyst, string path);
    public Task<bool> Validate(Analyst analyst, IFormFile file);
    public Task<bool> ProcessUploaded(Analyst analyst, string path);
    public Task<bool> ProcessUploaded(Analyst analyst, IFormFile file);
    
    Task<bool> ProcessUploadedGuest(string guestToken, IFormFile file);
    Task<CSVFile> GetFileGuest(string guestToken, string fileName);
    Task<List<CSVFile>> GetAllFilesGuest(string guestToken);                               
    Task RemoveFileGuest(string guestToken, string fileName);     
    
    public Task<TryChangeFieldTypeDTO> ChangeFieldType(CSVFile csvFile, int fieldIndex, string newType, char? listSplitChar);
    public Task<CSVFile> GetFile(Analyst analyst, string fileName);
    public Task<CSVFile> GetFile(int csvFileId);
    public Task<List<CSVFile>> GetAllFiles(Analyst analyst);
    public Task RemoveFile(Analyst analyst, string fileName);
    public Task RenameFile(Analyst analyst, string fileName, string newFileName);
    public Task<TableDisplayDTO> GetTableDisplayLimited(CSVFile csvFile, int rowLimit);
    public Task<TableDisplayDTO> GetTableDisplayWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit);
}