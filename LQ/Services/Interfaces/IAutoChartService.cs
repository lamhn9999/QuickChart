using LQ.Models.ChartModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface IAutoChartService
{
    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default);
    public List<Chart> DetectChart2(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default);
}