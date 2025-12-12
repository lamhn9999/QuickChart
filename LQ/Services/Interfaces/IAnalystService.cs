using LQ.Models.AnalystModels;

namespace LQ.Services.Interfaces;

public interface IAnalystService
{
    public Analyst? GetAnalyst(int id);
    public bool InsertAnalyst(Analyst analyst);
}