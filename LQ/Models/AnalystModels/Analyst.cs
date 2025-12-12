using LQ.Models.TableModels;

namespace LQ.Models.AnalystModels;

public class Analyst
{
    public int ID { get; set; }
    public string Name { get; set; }
    
    public ICollection<CSVFile> CSVFiles { get; set; }
}
