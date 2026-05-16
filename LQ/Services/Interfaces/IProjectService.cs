using LQ.Models.DTOs;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface IProjectService
{
    Task<List<ProjectListItemDTO>> List(string? analystId, string? guestToken);
    Task<ProjectDetailDTO?> GetByToken(Guid token, string? analystId, string? guestToken);

    Task<ProjectListItemDTO> Create(string name, string? analystId, string? guestToken);
    Task<bool> Rename(Guid token, string newName, string? analystId, string? guestToken);
    Task<bool> Delete(Guid token, string? analystId, string? guestToken);

    Task<UploadResultDTO?> UploadCSV(Guid projectToken, IFormFile file, string? analystId, string? guestToken);

    Task<ProjectChildDTO?> CreateCanvas(Guid projectToken, string? requestedName, string? analystId, string? guestToken);
    Task<bool> RenameCanvas(Guid canvasToken, string newName, string? analystId, string? guestToken);
    Task<bool> DeleteCanvas(Guid canvasToken, string? analystId, string? guestToken);

    Task<bool> RenameCSVFile(Guid fileToken, string newName, string? analystId, string? guestToken);
    Task<bool> DeleteCSVFile(Guid fileToken, string? analystId, string? guestToken);

    // Stores a client-rendered PNG snapshot of the canvas's current visual state.
    // Called by POST /workspace/canvas/{token}/snapshot after the document save settles.
    Task<bool> SetCanvasPreviewPng(Guid canvasToken, byte[] png, string? analystId, string? guestToken);
}
