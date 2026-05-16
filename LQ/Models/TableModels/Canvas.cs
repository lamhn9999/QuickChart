using System.Diagnostics.CodeAnalysis;

namespace LQ.Models.TableModels;

public class Canvas
{
    public int ID { get; private set; }
    public Guid PublicToken { get; private set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public int ProjectID { get; set; }
    public Project? Project { get; private set; }
    public int? LastUsedCSVFileID { get; set; }
    public CSVFile? LastUsedCSVFile { get; private set; }
    public required string Document { get; set; }
    public string? PreviewJson { get; set; }
    public required DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    protected Canvas() {} // For EF-Core

    [SetsRequiredMembers]
    public Canvas(int projectId, string name, string document)
    {
        ProjectID = projectId;
        Name = name;
        Document = document;
        UpdatedAt = DateTime.UtcNow;
    }
}
