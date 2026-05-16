using System.Diagnostics.CodeAnalysis;
using LQ.Models.AnalystModels;

namespace LQ.Models.TableModels;

public class Project
{
    public int ID { get; private set; }
    public Guid PublicToken { get; private set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public required DateTime ModifiedTime { get; set; } = DateTime.UtcNow;
    public string? AnalystID { get; private set; }
    public Analyst? Analyst { get; private set; }
    public string? GuestToken { get; private set; }

    public ICollection<CSVFile> CSVFiles { get; } = new HashSet<CSVFile>();
    public ICollection<Canvas> Canvases { get; } = new HashSet<Canvas>();

    protected Project() {} // For EF-Core

    [SetsRequiredMembers]
    public Project(string analystId, string name)
    {
        AnalystID = analystId;
        Name = name;
    }

    [SetsRequiredMembers]
    public Project(string guestToken, string name, bool isGuest)
    {
        GuestToken = guestToken;
        Name = name;
    }

    public void Touch() => ModifiedTime = DateTime.UtcNow;
}
