using System.Diagnostics.CodeAnalysis;

namespace LQ.Models.TableModels;

public class Canvas
{
    public int CSVFileID { get; private set; }
    public CSVFile? CSVFile { get; private set; }
    public required string Document { get; set; }
    public required DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    protected Canvas() {} // For EF Core

    [SetsRequiredMembers]
    public Canvas(int csvFileId, string document)
    {
        CSVFileID = csvFileId;
        Document = document;
        UpdatedAt = DateTime.UtcNow;
    }
}
