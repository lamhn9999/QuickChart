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
            fieldValues.Field.Type == typeof(DateTime))) result.Add(Category.Numeric);

        fieldValues.Field.Categories = result;
    }
    
    private Chart? DetectBarChart(Table table)
    {
        if (table.Columns.Count == 1) return null;
        bool isBar = false;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i].Field.Categories.Contains(Category.Categorical))
            {
                FieldValues temp = table.Columns[i];
                table.Columns[i] = table.Columns[0];
                table.Columns[0] = temp;
                isBar = true;
                break;
            }
        }
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
                double[] sumYValue = new double[barChart.X.Count];
                int[] cntYValue = new int[barChart.X.Count];
                for (int j = 0; j < table.RowCount; j++)
                {
                    object xLabel = table.Columns[0].Values[j].ValueCasted;
                    if (xLabel == null) continue;
                    if (fieldValues.Values[j].ValueCasted != null)
                    {
                        cntYValue[barChart.X.IndexOf(xLabel)] += 1;
                        sumYValue[barChart.X.IndexOf(xLabel)] += Convert.ToDouble(fieldValues.Values[j].ValueCasted);
                    }
                }

                for (int j = 0; j < sumYValue.Length; j++)
                {
                    labeldata.Data.Add(sumYValue[j] / (1.0 * cntYValue[j]));
                }

                barChart.Y.Add(labeldata);
            }

            return barChart;
        }
        return null;
    }

    private Chart? DetectPieChart(Table table)
    {
        if (table.Columns.Count != 1 ||
            !table.Columns[0].Field.Categories.Contains(Category.Categorical)) return null;
        Chart pieChart = new Chart();
        pieChart.ChartType = ChartType.Pie;
        
        Dictionary<object, int> cnt =  new Dictionary<object, int>();
        foreach (Value value in table.Columns[0].Values)
        {
            if (value.ValueCasted == null) continue;
            if (cnt.ContainsKey(value.ValueCasted)) cnt[value.ValueCasted] += 1;
            else cnt[value.ValueCasted] = 1;
        }
        
        pieChart.X = cnt.Select(kv => kv.Key).ToList();
        LabelData labelData = new LabelData();
        labelData.Label = "count";
        labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
        pieChart.Y.Add(labelData);
        return pieChart;
    }

    private Chart? DetectLineChart(Table table)
    {
        if (table.Columns.Count == 1) return null;
        bool isLine = false;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i].Field.Categories.Contains(Category.Categorical))
            {
                FieldValues temp = table.Columns[i];
                table.Columns[i] = table.Columns[0];
                table.Columns[0] = temp;
                isLine = true;
                break;
            }
        }
        
        for (int i = 1; i < table.Columns.Count; i++)
        {
            isLine &= table.Columns[i].Field.Categories.Contains(Category.Numeric);
        }

        if (isLine)
        {
            Chart LineChart = new Chart();
            LineChart.ChartType = ChartType.Line;
            
            List<object> XValueString = new List<object>();
            foreach (Value value in table.Columns[0].Values)
            {
                if (value.ValueCasted != null) XValueString.Add(value.ValueCasted);
            }
            LineChart.X = XValueString.Distinct().ToList();

            for (int i = 1; i < table.Columns.Count; i++)
            {
                FieldValues fieldValues = table.Columns[i];
                LabelData labeldata = new LabelData();
                labeldata.Label = fieldValues.Field.Name;
                double[] sumYValue = new double[LineChart.X.Count];
                int[] cntYValue = new int[LineChart.X.Count];
                for (int j = 0; j < table.RowCount; j++)
                {
                    object xLabel = table.Columns[0].Values[j].ValueCasted;
                    if (xLabel == null) continue;
                    if (fieldValues.Values[j].ValueCasted != null)
                    {
                        sumYValue[LineChart.X.IndexOf(xLabel)] += Convert.ToDouble(fieldValues.Values[j].ValueCasted);
                        cntYValue[LineChart.X.IndexOf(xLabel)] += 1;
                    }
                }

                for (int j = 0; j < cntYValue.Length; j++)
                {
                    labeldata.Data.Add(sumYValue[j] / (1.0 * cntYValue[j]));
                }

                LineChart.Y.Add(labeldata);
            }

            return LineChart;
        }
        return null;
    }

    private Chart? DetectPolarAreaChart(Table table)
    {
        if (table.Columns.Count != 1 ||
            !table.Columns[0].Field.Categories.Contains(Category.Categorical)) return null;
        Chart polarAreaChart = new Chart();
        polarAreaChart.ChartType = ChartType.PolarArea;
        
        Dictionary<object, int> cnt =  new Dictionary<object, int>();
        foreach (Value value in table.Columns[0].Values)
        {
            if (value.ValueCasted == null) continue;
            if (cnt.ContainsKey(value.ValueCasted)) cnt[value.ValueCasted] += 1;
            else cnt[value.ValueCasted] = 1;
        }
        
        polarAreaChart.X = cnt.Select(kv => kv.Key).ToList();
        LabelData labelData = new LabelData();
        labelData.Label = "count";
        labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
        polarAreaChart.Y.Add(labelData);
        return polarAreaChart;
    }

    private Chart? DetectHistogram(Table table)
    {
        if (table.Columns.Count != 1) return null;

        if (table.Columns[0].Field.Categories.Contains(Category.Numeric))
        {
            List<double> valueList = new List<double>();

            foreach (Value value in table.Columns[0].Values)
            {
                if (value.ValueCasted == null) continue;
                valueList.Add((double)value.ValueCasted);
            }

            valueList.Sort();
            double gap = ((valueList[valueList.Count - 1] - valueList[0]) / 10);
            if (gap == 0) gap = 1;
            Chart histogramChart = new Chart();
            histogramChart.ChartType = ChartType.Histogram;
            double xvalue = valueList[0] / gap * gap;
            while (xvalue < valueList[valueList.Count - 1])
            {
                histogramChart.X.Add(xvalue);
                xvalue += gap;
            }

            histogramChart.X.Add(xvalue);

            LabelData labelData = new LabelData();
            labelData.Label = table.Columns[0].Field.Name;
            int[] yValue = new int[histogramChart.X.Count - 1];
            foreach (Value value in table.Columns[0].Values)
            {
                for (int i = 0; i < histogramChart.X.Count - 1; i++)
                {
                    if (value.ValueCasted == null) continue;
                    if ((double)value.ValueCasted >= (double)histogramChart.X[i] &&
                        (double)value.ValueCasted < (double)histogramChart.X[i + 1])
                    {
                        yValue[i]++;
                        break;
                    }
                }
            }

            foreach (int yv in yValue)
            {
                labelData.Data.Add(yv);
            }

            histogramChart.Y.Add(labelData);
            return histogramChart;
        }

        if (table.Columns[0].Field.Categories.Contains(Category.Categorical))
        {
            Chart histogramChart = new Chart();
            histogramChart.ChartType = ChartType.Histogram;
            Dictionary<object, int> cnt =  new Dictionary<object, int>();
            foreach (Value value in table.Columns[0].Values)
            {
                if (value.ValueCasted == null) continue;
                if (cnt.ContainsKey(value.ValueCasted)) cnt[value.ValueCasted] += 1;
                else cnt[value.ValueCasted] = 1;
            }
        
            histogramChart.X = cnt.Select(kv => kv.Key).ToList();
            LabelData labelData = new LabelData();
            labelData.Label = "count";
            labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
            histogramChart.Y.Add(labelData);
            
            return histogramChart;
        }

        return null;
    }

    private Chart? DetectScatter(Table table)
    {
        if (table.Columns.Count != 2 ||
            !table.Columns[0].Field.Categories.Contains(Category.Numeric) ||
            !table.Columns[1].Field.Categories.Contains(Category.Numeric)) return null;
        
        Chart scatterChart = new Chart();
        scatterChart.ChartType = ChartType.Scatter;
        LabelData labelData = new LabelData();
        labelData.Label = (table.Columns[0].Field.Name + " vs " + table.Columns[1].Field.Name);
        for (int i = 0; i < table.Columns[0].Values.Count; i++)
        {
            if (table.Columns[0].Values[i].ValueCasted == null ||
                table.Columns[1].Values[i].ValueCasted == null) continue;
            labelData.Data.Add(new {x = table.Columns[0].Values[i].ValueCasted, 
                                y = table.Columns[1].Values[i].ValueCasted});
        }
        scatterChart.Y.Add(labelData);
        return scatterChart;
    }

    private Chart? DetectSideBar(Table table)
    {
        bool isBar = false;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i].Field.Categories.Contains(Category.Categorical) || table.Columns[i].Field.Categories.Contains(Category.Identifier))
            {
                FieldValues temp = table.Columns[i];
                table.Columns[i] = table.Columns[0];
                table.Columns[0] = temp;
                isBar = true;
                break;
            }
        }
        if (!isBar) return null;
        if (table.Columns.Count == 1)
        {
            Chart sideBarChart = new Chart();
            sideBarChart.ChartType = ChartType.SideBar;
            Dictionary<object, int> cnt =  new Dictionary<object, int>();
            foreach (Value value in table.Columns[0].Values)
            {
                if (value.ValueCasted == null) continue;
                if (cnt.ContainsKey(value.ValueCasted)) cnt[value.ValueCasted] += 1;
                else cnt[value.ValueStringFormatted] = 1;
            }

            var showData = cnt.OrderByDescending(kv => kv.Value).Take(20).ToList();
            sideBarChart.X = showData.Select(kv => kv.Key).ToList();
            LabelData labelData = new LabelData();
            labelData.Label = "count";
            labelData.Data = showData.Select(kv => kv.Value).Cast<object>().ToList();
            sideBarChart.Y.Add(labelData);
            return sideBarChart;
        }

        return null;
    }

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes)
    {
        Table tempTable = _lqDbService.GetTable(csvfileid, fieldindexes);
        
        List<Chart> res = new List<Chart>();
        
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
        {
            dir = dir.Parent;
        }
        var logPath = Path.Combine(dir!.FullName, "test.log");
        foreach(FieldValues fv in tempTable.Columns)
        {
            File.AppendAllText(logPath, JsonSerializer.Serialize(new {
                name = fv.Field.Name,
                categories = fv.Field.Categories,
                values = (from f in fv.Values select f.ValueCasted).ToList(),
                types = fv.Field.TypeName
            }) + "\n"
            );
        }
        
        // Bar chart
        Chart? barResult = DetectBarChart(tempTable);
        if (barResult != null) res.Add(barResult);
        // Pie chart
        Chart? pieResult = DetectPieChart(tempTable);
        if (pieResult != null) res.Add(pieResult);
        // Line chart
        Chart? lineResult = DetectLineChart(tempTable);
        if (lineResult != null) res.Add(lineResult);
        // Polar Area
        Chart? polarAreaResult = DetectPolarAreaChart(tempTable);
        if (polarAreaResult != null) res.Add(polarAreaResult);
        // Histogram
        Chart? histogramResult = DetectHistogram(tempTable);
        if (histogramResult != null) res.Add(histogramResult);
        // Scatter
        Chart? scatterResult = DetectScatter(tempTable);
        if (scatterResult != null) res.Add(scatterResult);
        // Side Bar
        Chart? sideBarResult = DetectSideBar(tempTable);
        if (sideBarResult != null) res.Add(sideBarResult);
        return res;
    }
}