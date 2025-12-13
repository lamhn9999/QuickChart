using LQ.Services.Interfaces;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;

namespace LQ.Services.Implementations;

public class AnalystService : IAnalystService
{
    private readonly ILQDbService _lqdbService;

    public AnalystService(ILQDbService lqdbService)
    {
        _lqdbService = lqdbService;
    }

    public Analyst? GetAnalyst(string name)
    {
        return _lqdbService.GetAnalyst(name);
    }

    public Analyst? GetAnalyst(int id)
    {
        return _lqdbService.GetAnalyst(id);
    }

    public List<CSVFile> GetCSVFiles(int analystid)
    {
        return _lqdbService.GetCSVFiles(analystid);
    }
}