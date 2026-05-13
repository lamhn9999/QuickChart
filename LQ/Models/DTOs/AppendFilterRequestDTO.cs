namespace LQ.Models.DTOs;

/// <summary>
/// Body for POST /workspace/canvas/append-filter. Mirrors AppendChartRequestDTO
/// in shape — target file token + a typed spec — so the controller flow stays
/// symmetric.
///
/// v1 contract: a filter card binds to a single field index from its **source**
/// file. The source file is identified by <see cref="AppendFilterSpec.CsvFileToken"/>
/// — without it, the persisted block would re-interpret <c>fieldIndex</c> against
/// the target file's schema and silently filter the wrong column when the two
/// schemas don't match. The target's hydrate path uses the persisted token to
/// fetch distinct values from the right file, and to limit auto-applied charts
/// to those that share the same source file.
/// </summary>
public record AppendFilterRequestDTO
{
    public required Guid TargetFileToken { get; init; }
    public required AppendFilterSpec FilterSpec { get; init; }
}

public record AppendFilterSpec
{
    /// <summary>
    /// Source file the filter's <see cref="FieldIndex"/> is meaningful against.
    /// Required: even when copying within the same canvas, the block stores it
    /// so the document remains self-describing if later re-copied.
    /// </summary>
    public required Guid CsvFileToken { get; init; }
    public required int FieldIndex { get; init; }
    public List<string?>? SelectedValues { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
}
