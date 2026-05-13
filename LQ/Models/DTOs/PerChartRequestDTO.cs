namespace LQ.Models.DTOs;

// Body shape for POST /workspace/chart.
// Unlike AutoChartRequestDTO, the filters arrive inline (request-scoped) and the
// endpoint deliberately ignores HttpContext.Session["ListValueFilterDTO"] so a
// single chart can be filtered independently of the table-wide filter bag.
public record PerChartRequestDTO
{
    public Guid? CsvFileToken { get; init; }
    public required List<int> FieldIndexes { get; init; }
    public List<ValueFilterDTO>? InlineFilters { get; init; }
}
