using LQ.Models.ChartModels;
using LQ.Models.TableModels;

namespace LQ.Services.Interfaces;

public interface IAutoChartService
{
    public void DetectCategory(FieldValues fieldValues);
    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes);
}