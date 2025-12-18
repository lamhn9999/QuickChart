using LQ.Models.AnalystModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface IAnalystService
{
    public Analyst? GetAnalyst(string name);
    public Analyst? GetAnalyst(int id);
    public bool InsertAnalyst(Analyst analyst);
    public List<CSVFile> GetCSVFiles(int analystid);
}