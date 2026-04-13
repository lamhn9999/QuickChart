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
}