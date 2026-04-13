using System.Configuration;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface IAutoChartService
{
    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default);
    public Task<List<Chart>> DetectChartAsync(int csvfileid, List<int> fieldindexes,
        CancellationToken cancellationToken = default);
    public Task<List<Chart>> DetectChartWithFiltersAsync(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters, CancellationToken cancellationToken = default);
}