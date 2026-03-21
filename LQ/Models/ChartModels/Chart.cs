using System.Text.Json;

namespace LQ.Models.ChartModels;

public enum ChartType 
{
    Pie,
    Bar,
    Line,
    Histogram,
    Scatter,
    PolarArea,
    SideBar,
    Mixed,
    Matrix,
    MixedBar,
    Bubble,
}

public class LabelData
{
    public ChartType? ChartType { get; set; }
    public string Label { get; set; } = "";
    public List<object> Data { get; set; } = new();
}

public class Chart
{
    public string Title { get; set; }
    public ChartType ChartType {get; set;}
    public List<object> X { get; set; } = new();
    public List<LabelData> Y { get; set; } = new();
    public List<LabelData> Z { get; set; } = new();
    public int RelationshipCount  => ChartType is ChartType.Pie or ChartType.Histogram or ChartType.PolarArea
        ? 1
        : Y.Count() + Z.Count();

    public int PanelIndex { get; set; } = -1;
    public string ShowJson()
    {
        return JsonSerializer.Serialize(this);
    }
}