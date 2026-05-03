using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
namespace LQ.Services.Interfaces;

public interface ICSVService
{
    public Task<bool> Validate(Analyst analyst, string path);
    public Task<bool> Validate(Analyst analyst, IFormFile file);
    public Task<UploadResultDTO?> ProcessUploaded(Analyst analyst, string path);
    public Task<UploadResultDTO?> ProcessUploaded(Analyst analyst, IFormFile file);

    Task<UploadResultDTO?> ProcessUploadedGuest(string guestToken, IFormFile file);
    // Background continuation invoked from a fresh DI scope after the kickoff
    // has persisted the CSVFile placeholder and returned to the user.
    Task ContinueProcessAsync(Guid token, byte[] fileBytes, bool useSinglePass);
    // Returns false if the file doesn't exist or doesn't belong to the caller.
    Task<bool> ConfirmListFields(Guid token, string? analystId, string? guestToken, List<ConfirmFieldDTO> confirmed);
    Task<List<PendingUploadDTO>> GetPendingUploads(string? analystId, string? guestToken);
    Task<CSVFile> GetFileGuest(string guestToken, string fileName);
    Task<List<CSVFile>> GetAllFilesGuest(string guestToken);                               
    Task RemoveFileGuest(string guestToken, string fileName);     
    
    public Task<TryChangeFieldTypeDTO> ChangeFieldType(CSVFile csvFile, int fieldIndex, string newType, char? listSplitChar);
    public Task<string> ChangeFieldCategories(CSVFile csvFile, int fieldIndex, List<Category> newCategories);
    public Task<CSVFile> GetFile(Analyst analyst, string fileName);
    public Task<CSVFile> GetFile(int csvFileId);
    public Task<CSVFile?> GetFile(Guid publicToken);
    public Task<List<CSVFile>> GetAllFiles(Analyst analyst);
    public Task RemoveFile(Analyst analyst, string fileName);
    public Task RenameFile(Analyst analyst, string fileName, string newFileName);
    public Task<TableDisplayDTO> GetTableDisplayLimited(CSVFile csvFile, int rowLimit);
    public Task<TableDisplayDTO> GetTableDisplayWithFilterLimited(CSVFile csvFile, List<ValueFilterDTO> filters, int rowLimit);
}