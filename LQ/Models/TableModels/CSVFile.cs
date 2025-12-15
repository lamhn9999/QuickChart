using System.Diagnostics.CodeAnalysis;
using LQ.Models.AnalystModels;
namespace LQ.Models.TableModels;

public class CSVFile
{
    public int ID { get; private set; }
    public required string Name { get; set; }
    public required DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public required DateTime ModifiedTime { get; set; } = DateTime.UtcNow;
    public Analyst Analyst { get; private set; }
    protected CSVFile() {} // For EF-Core

    [SetsRequiredMembers]
    public CSVFile(Analyst analyst, string filename)
    {
        Name = filename;
        Analyst = analyst;
    }
    [SetsRequiredMembers]
    public CSVFile(Analyst analyst, string filename, DateTime createdTime) : this(analyst, filename)
    {
        CreatedTime = createdTime;
    }
}