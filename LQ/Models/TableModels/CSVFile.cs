using LQ.Models.AnalystModels;
namespace LQ.Models.TableModels;

public class CSVFile
{
    public int ID { get; set; }
    public string Name { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    
    public int AnalystID { get; set; }
    public Analyst Analyst { get; set; }
}