namespace LQ.Models.ChartModels;

public enum ChartType 
{
    Pie,
    Bar,
    Line,
    Histogram
}

public class LabelData
{
    public string Label { get; set; } = "";
    public List<object> Data { get; set; } = new();
}

public class Chart
{
    public ChartType ChartType {get; set;}
    public List<object> X { get; set; } = new();
    public List<LabelData> Y { get; set; } = new();
    public List<LabelData> Z { get; set; } = new();
}