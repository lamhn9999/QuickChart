namespace LQ.Models.DTOs;

public record TryChangeFieldTypeDTO(IEnumerable<string>? ActualMismatchedValues)
{
    private static readonly int MismatchedValuesDisplayThreshold = 100;
    public bool Status => MismatchedCount == 0;
    public int MismatchedCount => ActualMismatchedValues?.Count() ?? 0;
    public List<string> MismatchedValues => ActualMismatchedValues?.Take(MismatchedValuesDisplayThreshold).ToList() ?? new();
}