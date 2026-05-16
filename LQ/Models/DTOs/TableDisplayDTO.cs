using LQ.Models.TableModels;

namespace LQ.Models.DTOs;

public record TableFieldDTO
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    
    public required IReadOnlyList<Category> Categories { get; init; }
    public int Index { get; init; }
}

public record TableDisplayDTO
{
    public required string FileName { get; init; }
    public required List<TableFieldDTO> Fields { get; init; }
    public required string[][] Values { get; init; }
    public int RowCount => Values.Length;
    public string? SavedCanvasJson { get; init; }

    // Phase 4 — populated when the workspace is entered via /workspace/{canvasToken}.
    // Null on the legacy session-keyed entry so the existing view path is unaffected.
    public Guid? CanvasToken { get; init; }
    public string? CanvasName { get; init; }
    public Guid? ActiveCSVToken { get; init; }
    public IReadOnlyList<WorkspaceSiblingDTO> Siblings { get; init; } = Array.Empty<WorkspaceSiblingDTO>();
}

public record WorkspaceSiblingDTO(Guid Token, string Name);