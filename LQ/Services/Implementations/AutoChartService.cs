using System.Text.Json;
using LQ.Models.ChartModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace LQ.Services.Implementations;

public class AutoChartService : IAutoChartService
{
    private readonly ILQDbService _lqDbService;
    public AutoChartService(ILQDbService lqDbService)
    {
        _lqDbService = lqDbService;
    }
    public void DetectCategory(FieldValues fieldValues)
    {
        List <Category> result =  new List<Category>();
        List<string> valueStringlist = new List<string>();
        foreach (Value value in fieldValues.Values)
        {
            if (string.IsNullOrWhiteSpace(value.ValueString)) continue;
            valueStringlist.Add(value.ValueString.Trim());
        }

        double uniqPercentage = (1.0 * valueStringlist.Distinct().ToList().Count) / (fieldValues.Values.Count * 1.0);
        
        // Categorical
        if (uniqPercentage <= Field.CATEGORICAL_THRESHOLD &&
            (fieldValues.Field.Type == typeof(string) || fieldValues.Field.Type == typeof(int)) ||
            fieldValues.Field.Type == typeof(bool)) result.Add(Category.Categorical);
        
        // Identifier
        if ((uniqPercentage >= Field.IDENTIFIER_THRESHOLD &&
            (fieldValues.Field.Type == typeof(string) || 
             fieldValues.Field.Type == typeof(int))) ||
            (fieldValues.Field.Type == typeof(string) && !result.Contains(Category.Categorical))) result.Add(Category.Identifier);
        
        // Numeric
        if ((fieldValues.Field.Type == typeof(int) || 
            fieldValues.Field.Type == typeof(double) || 
            fieldValues.Field.Type == typeof(DateTime)) && 
            !result.Contains(Category.Categorical)) result.Add(Category.Numeric);

        fieldValues.Field.Categories = result;
    }

    private Chart? DetectBarChart(Table table)
    {
        bool isBar = true;
        isBar &= table.Columns[0].Field.Categories.Contains(Category.Categorical);
        for (int i = 1; i < table.Columns.Count; i++)
        {
            isBar &= table.Columns[i].Field.Categories.Contains(Category.Numeric);
        }

        if (isBar)
        {
            Chart barChart = new Chart();
            barChart.ChartType = ChartType.Bar;
            
            List<object> XValueString = new List<object>();
            foreach (Value value in table.Columns[0].Values)
            {
                if (value.ValueCasted != null) XValueString.Add(value.ValueCasted);
            }
            barChart.X = XValueString.Distinct().ToList();

            for (int i = 1; i < table.Columns.Count; i++)
            {
                FieldValues fieldValues = table.Columns[i];
                LabelData labeldata = new LabelData();
                labeldata.Label = fieldValues.Field.Name;
                float[] sumYValue = new float[barChart.X.Count];
                for (int j = 0; j < table.RowCount; j++)
                {
                    object xLabel = table.Columns[0].Values[j].ValueCasted;
                    if (xLabel == null) continue;
                    if (fieldValues.Values[j].ValueCasted != null)
                        sumYValue[barChart.X.IndexOf(xLabel)] += (float)fieldValues.Values[j].ValueCasted;
                }

                foreach (float value in sumYValue)
                {
                    labeldata.Data.Add(value / table.RowCount);
                }

                barChart.Y.Add(labeldata);
            }

            return barChart;
        }
        return null;
    }

    private Chart? DetectPieChart(Table table)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
        {
            dir = dir.Parent;
        }
        var logPath = Path.Combine(dir!.FullName, "test.log");
        foreach(FieldValues fv in table.Columns)
        {
            File.AppendAllText(logPath, JsonSerializer.Serialize(new {
                name = fv.Field.Name,
                categories = fv.Field.Categories,
                values = (from f in fv.Values select f.ValueString).ToList()
            })
            );
        }

        if (table.Columns.Count != 1 ||
            !table.Columns[0].Field.Categories.Contains(Category.Categorical)) return null;
        Chart pieChart = new Chart();
        pieChart.ChartType = ChartType.Pie;
        
        List<object> XValueString = new List<object>();
        foreach (Value value in table.Columns[0].Values)
        {
            File.AppendAllText(logPath, value.ValueString is null ? "null" : value.ValueString);
            if (value.ValueCasted != null)
            {
                XValueString.Add(value.ValueCasted);
            }
        }
        
        pieChart.X = XValueString.Distinct().ToList();

        LabelData labelData = new LabelData();
        labelData.Label = table.Columns[0].Field.Name;
        int[] cntLabel = new int[pieChart.X.Count];
        foreach (Value value in table.Columns[0].Values)
        {
            if (value.ValueCasted != null) cntLabel[pieChart.X.IndexOf(value.ValueCasted)] += 1;
        }
        
        foreach (int value in cntLabel)
        {
            File.AppendAllText(logPath, value.ToString() + '\n');
            labelData.Data.Add(value);
        }
        pieChart.Y.Add(labelData);
        return pieChart;
    }

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes)
    {
        Table tempTable = _lqDbService.GetTable(csvfileid, fieldindexes);
        
        List<Chart> res = new List<Chart>();
        
        // Bar chart
        Chart? barResult = DetectBarChart(tempTable);
        if (barResult != null) res.Add(barResult);
        // Pie chart
        Chart? pieResult = DetectPieChart(tempTable);
        if (pieResult != null) res.Add(pieResult);
        return res;
    }
}