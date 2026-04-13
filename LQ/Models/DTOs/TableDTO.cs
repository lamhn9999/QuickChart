namespace LQ.Models.DTOs;

public record TableDTO
{
    public int RowCount { get; init; }
    public int FieldCount { get; init; }
}