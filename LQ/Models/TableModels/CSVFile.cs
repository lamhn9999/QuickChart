using System.Diagnostics.CodeAnalysis;
using LQ.Models.AnalystModels;
namespace LQ.Models.TableModels;

public class CSVFile
{
    public int ID { get; private set; }
    public Guid PublicToken { get; private set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public required DateTime ModifiedTime { get; set; } = DateTime.UtcNow;
    public string? AnalystID { get; private set; }
    public Analyst? Analyst { get; private set; }
    public string? GuestToken { get; private set; }
    public UploadStatus Status { get; set; } = UploadStatus.Processing;
    public string? ProcessingPayload { get; set; }
    public int ProjectID { get; set; }
    public Project? Project { get; private set; }
    protected CSVFile() {} // For EF-Core

    [SetsRequiredMembers]
    public CSVFile(string analystId, string fileName)
    {
        Name = fileName;
        AnalystID = analystId;
    }

    [SetsRequiredMembers]
    public CSVFile(string guestToken, string fileName, bool isGuest)
    {
        Name = fileName;
        GuestToken = guestToken;
    }

    public bool NameCheck(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        if (filename.IndexOfAny(invalidChars) >= 0)
            return false;

        if (!filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return false;

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            return false;

        return true;
    }
}