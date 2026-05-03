namespace LQ.Models.DTOs;

public record AppendChartRequestDTO
{
    public required Guid TargetFileToken { get; init; }
    public required AppendChartSpec ChartSpec { get; init; }
}

public record AppendChartSpec
{
    public required Guid CsvFileToken { get; init; }
    public required List<int> FieldIndexes { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
}
