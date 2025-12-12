using LQ.Services.Interfaces;
using LQ.Models.AnalystModels;

namespace LQ.Services.Implementations;

public class AnalystService : IAnalystService
{
    private readonly ILQDbService _lqdbService;

    public AnalystService(ILQDbService lqdbService)
    {
        _lqdbService = lqdbService;
    }

    public Analyst? GetAnalyst(int id)
    {
        return _lqdbService.GetAnalyst(id);
    }

    public bool InsertAnalyst(Analyst analyst)
    {
        if (GetAnalyst(analyst.ID) is not null) return false;
        _lqdbService.InsertAnalyst(analyst);
        return true;
    }
}