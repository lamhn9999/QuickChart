using System.Diagnostics.CodeAnalysis;
using LQ.Models.AnalystModels;
namespace LQ.Models.TableModels;

public class CSVFile
{
    public int ID { get; private set; }
    public required string Name { get; set; }
    public required DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    public required DateTime ModifiedTime { get; set; } = DateTime.UtcNow;
    public string AnalystID { get; private set; }
    public Analyst Analyst { get; private set; }
    protected CSVFile() {} // For EF-Core

    [SetsRequiredMembers]
    public CSVFile(string analystid, string filename)
    {
        Name = filename;
        AnalystID = analystid;
    }
    [SetsRequiredMembers]
    public CSVFile(string analystid, string filename, DateTime createdTime) : this(analystid, filename)
    {
        CreatedTime = createdTime;
    }
}