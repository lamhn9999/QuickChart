namespace LQ.Models.DTOs;

public record AutoChartRequestDTO
{
    public Guid? CsvFileToken { get; init; }
    public required List<int> FieldIndexes { get; init; }
}
