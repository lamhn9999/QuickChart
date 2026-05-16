namespace LQ.Models.DTOs;

// Wire format only — Project.ID stays server-side; PublicToken is the handle.
public record ProjectListItemDTO(
    Guid Token,
    string Name,
    DateTime CreatedTime,
    DateTime ModifiedTime,
    int CSVCount,
    int CanvasCount);

public record ProjectDetailDTO(
    Guid Token,
    string Name,
    DateTime CreatedTime,
    DateTime ModifiedTime,
    List<ProjectChildDTO> CSVFiles,
    List<ProjectChildDTO> Canvases);

public enum ProjectChildKind { CSV, Canvas }

public record ProjectChildDTO(
    ProjectChildKind Kind,
    Guid Token,
    string Name,
    DateTime ModifiedTime,
    // CSV-only metadata
    int? RowCount,
    int? FieldCount,
    string? Status,
    // CSV-only — first up-to-5 rows × up-to-4 cols of real data for the card preview
    List<string>? PreviewHeaders,
    List<List<string>>? PreviewRows,
    // Canvas-only — true if a snapshot PNG is stored (served via /projects/canvas/{token}/preview.png)
    bool HasPreview);

public record CreateProjectRequestDTO(string Name);
public record RenameProjectRequestDTO(string Name);
public record RenameCanvasRequestDTO(string Name);
public record CreateCanvasRequestDTO(string? Name);
public record RenameCSVRequestDTO(string Name);
