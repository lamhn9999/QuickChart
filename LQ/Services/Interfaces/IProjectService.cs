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

    // Regenerates the SVG silhouette from the canvas's current Document JSON
    // and stores it on the row. Called by SaveCanvas after each write.
    Task RegenerateCanvasPreview(Guid canvasToken);
}
