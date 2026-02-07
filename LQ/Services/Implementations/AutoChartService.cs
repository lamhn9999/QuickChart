using System.Text.Json;
using System;
using System.Diagnostics.Metrics;
using System.Net;
using LQ.Models.ChartModels;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Table = LQ.Models.TableModels.Table;

namespace LQ.Services.Implementations;

public class AutoChartService : IAutoChartService
{
    private readonly ILQDbService _lqDbService;
    public AutoChartService(ILQDbService lqDbService)
    {
        _lqDbService = lqDbService;
    }

    private int CountDistinctValues(FieldValues fv)
    {
        Dictionary<object, int> cnt = new Dictionary<object, int>();
        Index(cnt, fv);
        return cnt.Count;
    }

    private void Index(Dictionary<object, int> cnt, FieldValues table)
    {
        foreach (var value in table.Values)
        {
            if (value.ValueCasted == null)
                continue;

            if (table.Field.Type == typeof(List<string>))
            {
                foreach (var f in (List<string>)value.ValueCasted)
                {
                    if (f == null) continue;
                    cnt[f] = cnt.TryGetValue(f, out var n) ? n + 1 : 1;
                }
            }
            else
            {
                cnt[value.ValueCasted] = cnt.TryGetValue(value.ValueCasted, out var n) ? n + 1 : 1;
            }
        }
    }
    private DateTime ConvertToDateTime(long val)
    {
        return new DateTime(val, DateTimeKind.Utc).Date;
    }

    private string GetYear(object val)
    {
        return ConvertToDateTime((long)val).Year.ToString();
    }

    private Chart? DetectBarChart(List<FieldValues> table)
    {
        if (table.Count == 1) return null;
        Chart barChart = new Chart();
        barChart.ChartType = ChartType.Bar; 
        
        Dictionary<(object x, object y), int> dict2C = new(); 
        Dictionary<object, (double sum, int cnt)> prep = new Dictionary<object, (double sum, int cnt)>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            
            if (table[0].Field.Type == typeof(List<string>))
            {
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (!prep.TryGetValue(f, out var sc)) prep[f] = (0, 0);
                    if (table[1].Field.Categories.Contains(Category.Categorical))
                    {
                        if (table[1].Field.Type == typeof(List<string>))
                        {
                            foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                            {
                                if (f1 is null) continue;
                                if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                                else dict2C[(f, f1)]++;
                            }
                        }
                        else
                        {
                            var f1 = table[1].Values[i].ValueCasted;
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                            else dict2C[(f, f1)]++;
                        }
                    }
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (!prep.TryGetValue(key, out var sc)) prep[key] = (0, 0);
                if (table[1].Field.Categories.Contains(Category.Categorical))
                {
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        if (table[1].Values[i].ValueCasted == null) continue;
                        foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                        {
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                            else dict2C[(key, f1)]++;
                        }
                    }
                    else
                    {
                        var f1 = table[1].Values[i].ValueCasted;
                        if (f1 is null) continue;
                        if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                        else dict2C[(key, f1)]++;
                    }
                }
            }
        }

        barChart.X = prep.OrderBy(x => x.Key).Select(kv => kv.Key).ToList();
        // if (barChart.X.Count > 20) return null;
        
        for (int j = 1; j < table.Count; j++)
        {
            if (table[j].Field.Categories.Contains(Category.Numeric))
            {
                Dictionary<object, (double sum, int cnt)> avg = new Dictionary<object, (double sum, int cnt)>(prep);
                for (int i = 0; i < table[0].Values.Count; i++)
                {
                    var val = table[j].Values[i].ValueCasted;
                    if (table[0].Values[i].ValueCasted == null) continue;
                    if (val != null)
                    {
                        if (table[0].Field.Type == typeof(List<String>))
                        {
                            foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                            {
                                if (f == null) continue;
                                avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1);
                            }
                        }
                        else
                        {
                            var key = table[0].Values[i].ValueCasted;
                            if (key == null) continue;
                            avg[key] = (avg[key].sum + Convert.ToDouble(val), avg[key].cnt + 1);
                        }
                    }
                }
                var data = avg.Select(kv => new { Key = kv.Key, Val = (kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / (1.0 * kv.Value.cnt)) })
                    .OrderBy(x => x.Key)
                    .ToList();
                LabelData labelData = new LabelData();
                labelData.Label = $"Mean of \"{table[j].Field.Name}\" by \"{table[0].Field.Name}\"";
                labelData.Data = data.Select(kv => kv.Val).Cast<object>().ToList();
                barChart.Y.Add(labelData);
            }
        }
        
        var xs = dict2C.Keys
            .Select(k => k.x)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var ys = dict2C.Keys
            .Select(k => k.y)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var countsByY = ys.ToDictionary(
            y => y,
            y => xs.Select(x => dict2C.GetValueOrDefault((x, y), 0))
                .ToList()
        );
        foreach (var kv in countsByY)
        {
            LabelData labelData = new LabelData();
            labelData.Label = $"Count of \"{kv.Key}\" by \"{table[0].Field.Name}\"";
            labelData.Data = kv.Value.Select(v => (object)v).ToList();
            barChart.Y.Add(labelData);
        }

        return barChart;
    }

    private Chart? DetectPieChart(List<FieldValues> table)
    {
        Chart pieChart = new Chart();
        pieChart.ChartType = ChartType.Pie;
        
        Dictionary<object, int> cnt =  new Dictionary<object, int>();
        Index(cnt, table[0]);
        
        pieChart.X = cnt.Select(kv => kv.Key).ToList();
        LabelData labelData = new LabelData();
        labelData.Label = $"Count of \"{table[0].Field.Name}\"";
        labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
        pieChart.Y.Add(labelData);
        return pieChart;
    }

    private Chart? DetectLineChart(List<FieldValues> table)
    {
        Chart LineChart = new Chart();
        LineChart.ChartType = ChartType.Line;

        Dictionary<(object x, object y), int> dict2C = new();
        Dictionary<object, (double sum, int cnt)> prep = new Dictionary<object, (double sum, int cnt)>();
        // Setup
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;

            if (table[0].Field.Type == typeof(List<string>))
            {
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (!prep.TryGetValue(f, out var sc)) prep[f] = (0, 0);
                    if (table[1].Field.Categories.Contains(Category.Categorical))
                    {
                        if (table[1].Field.Type == typeof(List<string>))
                        {
                            if (table[1].Values[i].ValueCasted == null) continue;
                            foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                            {
                                if (f1 is null) continue;
                                if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                                else dict2C[(f, f1)]++;
                            }
                        }
                        else
                        {
                            var f1 = table[1].Values[i].ValueCasted;
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                            else dict2C[(f, f1)]++;
                        }
                    }
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (!prep.TryGetValue(key, out var sc)) prep[key] = (0, 0);
                if (table[1].Field.Categories.Contains(Category.Categorical))
                {
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        if (table[1].Values[i].ValueCasted == null) continue;
                        foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                        {
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                            else dict2C[(key, f1)]++;
                        }
                    }
                    else
                    {
                        var f1 = table[1].Values[i].ValueCasted;
                        if (f1 is null) continue;
                        if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                        else dict2C[(key, f1)]++;
                    }
                }
            }
        }
        LineChart.X = prep.OrderBy(x => x.Key).Select(kv => kv.Key).ToList();
        // 1 categorical
        if (table[1].Field.Categories.Contains(Category.Numeric))
        {
            for (int j = 1; j < table.Count; j++)
            {
                if (table[j].Field.Categories.Contains(Category.Numeric))
                {   
                    Dictionary<object, (double sum, int cnt)> avg = new Dictionary<object, (double sum, int cnt)>(prep);
                    for (int i = 0; i < table[0].Values.Count; i++)
                    {
                        var val = table[j].Values[i].ValueCasted;
                        if (table[0].Values[i].ValueCasted == null) continue;
                        if (val != null)
                        {
                            if (table[0].Field.Type == typeof(List<string>))
                            {
                                foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                                {
                                    if (f == null) continue;
                                    avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1);
                                }
                            }
                            else
                            {
                                var key = table[0].Values[i].ValueCasted;
                                if (key == null) continue;
                                avg[key] = (avg[key].sum + Convert.ToDouble(val), avg[key].cnt + 1);
                            }
                        }
                    }

                    var data = avg.Select(kv => new { Key = kv.Key, Val = (kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / (1.0 * kv.Value.cnt)) })
                        .OrderBy(x => x.Key)
                        .ToList();
                    LabelData labelData = new LabelData();
                    labelData.Label = $"Mean of \"{table[j].Field.Name}\" by \"{table[0].Field.Name}\"";
                    labelData.Data = data.Select(kv => kv.Val).Cast<object>().ToList();
                    LineChart.Y.Add(labelData);
                }
            }
            return LineChart;
        }
        // 2 categoricals
        var xs = dict2C.Keys
            .Select(k => k.x)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var ys = dict2C.Keys
            .Select(k => k.y)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var countsByY = ys.ToDictionary(
            y => y,
            y => xs.Select(x => dict2C.GetValueOrDefault((x, y), 0))
                .ToList()
        );
        foreach (var y in ys)
        {   
            for (int j = 2; j < table.Count; j++)
            {
                Dictionary<object, (double sum, int cnt)> avg = new Dictionary<object, (double sum, int cnt)>(prep);
                for (int i = 0; i < table[0].Values.Count; i++)
                {
                    // if (!Equals(table[1].Values[i].ValueCasted, y)) continue;
                    // if (Values[i].value!y.Equals(table[1].Values[i].ValueCasted)) continue;
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        if (table[1].Values[i].ValueCasted == null) continue;
                        bool exist = false;
                        foreach (var f in (List<string>)table[1].Values[i].ValueCasted)
                        {
                            if (f == null) continue;
                            if (y.Equals(f))
                            {
                                exist = true;
                                break;
                            }
                        }

                        if (!exist) continue;
                    }
                    else
                    {
                        if (!y.Equals(table[1].Values[i].ValueCasted)) continue;
                    }
                    var val = table[j].Values[i].ValueCasted;
                    if (table[0].Values[i].ValueCasted == null) continue;
                    if (val != null)
                    {
                        if (table[0].Field.Type == typeof(List<string>))
                        {
                            foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                            {
                                if (f == null) continue;
                                avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1);
                            }
                        }
                        else
                        {
                            var key = table[0].Values[i].ValueCasted;
                            if (key == null) continue;
                            avg[key] = (avg[key].sum + Convert.ToDouble(val), avg[key].cnt + 1);
                        }
                    }
                }

                var data = avg.Select(kv => new { Key = kv.Key, Val = (kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / (1.0 * kv.Value.cnt)) })
                    .OrderBy(x => x.Key)
                    .ToList();
                LabelData labelData = new LabelData();
                labelData.Label = $"Mean of \"{table[j].Field.Name}\" where {table[1].Field.Name} = {y.ToString()} by \"{table[0].Field.Name}\"";
                labelData.Data = data.Select(kv => kv.Val).Cast<object>().ToList();
                labelData.ChartType = ChartType.Line;
                LineChart.Z.Add(labelData);
            }
        }

        if (LineChart.Z.Count != 0) LineChart.ChartType = ChartType.Mixed;
        foreach (var kv in countsByY)
        {
            LabelData labelData = new LabelData();
            labelData.Label = $"Count of \"{kv.Key}\" by \"{table[0].Field.Name}\"";
            labelData.Data = kv.Value.Select(v => (object)v).ToList();
            labelData.ChartType = ChartType.Bar;
            LineChart.Y.Add(labelData);
        }

        return LineChart;
    }

    private Chart? DetectPolarAreaChart(List<FieldValues> table)
    {
        Chart polarAreaChart = new Chart();
        polarAreaChart.ChartType = ChartType.PolarArea;
        
        Dictionary<object, int> cnt =  new Dictionary<object, int>();
        Index(cnt, table[0]);
        
        polarAreaChart.X = cnt.Select(kv => kv.Key).ToList();
        LabelData labelData = new LabelData();
        labelData.Label = $"Count of \"{table[0].Field.Name}\"";
        labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
        polarAreaChart.Y.Add(labelData);
        return polarAreaChart;
    }
    private Chart? DetectHistogram(List<FieldValues> table)
    {
        if (table[0].Field.Categories.Contains(Category.Numeric))
        {
            List<double> valueList = new List<double>();
        
            foreach (Value value in table[0].Values)
            {
                if (value.ValueCasted == null) continue;
                valueList.Add(Convert.ToDouble(value.ValueCasted));
            }
            valueList.Sort();
            double q1 = valueList[Math.Max((valueList.Count + 1) / 4 - 1, 0)];
            double q3 = valueList[valueList.Count - 1 - (valueList.Count + 1) / 4];
            double iqr = q3 - q1;
            double gap = (iqr <= 0) ? (valueList[valueList.Count - 1] - valueList[0]) / Math.Max(1, (int)Math.Sqrt(valueList.Count)) : 2.0 * iqr / Math.Pow(valueList.Count, 1.0 / 3.0);
            if (gap == 0) gap = 1;
            Chart histogramChart = new Chart();
            histogramChart.ChartType = ChartType.Histogram;
            double xvalue = valueList[0];
            List<double> listXValue = new List<double>();
            while (xvalue < valueList[valueList.Count - 1])
            {
                listXValue.Add(xvalue);
                if (table[0].Field.Type == typeof(DateTime))
                    histogramChart.X.Add(
                        $"[{ConvertToDateTime((long)xvalue)},{ConvertToDateTime((long)(xvalue + gap))})");
                else histogramChart.X.Add($"[{xvalue}, {Math.Round(xvalue + gap, 2)})");
                xvalue = Math.Round(xvalue + gap, 2);
            }
            listXValue.Add(xvalue);
            LabelData labelData = new LabelData();
            labelData.Label = $"Distribution of \"{table[0].Field.Name}\"";
            int[] yValue = new int[histogramChart.X.Count];
            foreach (double value in valueList)
            {
                for (int i = 0; i < histogramChart.X.Count; i++)
                {
                    if (value >= listXValue[i] &&
                        value < listXValue[i + 1])
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

        if (table[0].Field.Categories.Contains(Category.Categorical))
        {
            Chart histogramChart = new Chart();
            histogramChart.ChartType = ChartType.Histogram;
            Dictionary<object, int> cnt =  new Dictionary<object, int>();
            Index(cnt, table[0]);
            histogramChart.X = cnt.Select(kv => kv.Key).ToList();
            if (histogramChart.X.Count > 20) return null;
            LabelData labelData = new LabelData();
            labelData.Label = $"Count of \"{table[0].Field.Name}\"";
            labelData.Data = cnt.Select(kv => kv.Value).Cast<object>().ToList();
            histogramChart.Y.Add(labelData);
            
            return histogramChart;
        }

        return null;
    }

    private Chart? DetectScatter(List<FieldValues> table)
    {
        Chart scatterChart = new Chart();
        scatterChart.ChartType = ChartType.Scatter;
        LabelData labelData = new LabelData();
        labelData.Label = $"Relationship between \"{table[0].Field.Name}\" and \"{table[1].Field.Name}\"";
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null ||
                table[1].Values[i].ValueCasted == null) continue;
            labelData.Data.Add(new {x = table[0].Values[i].ValueCasted, 
                                y = table[1].Values[i].ValueCasted});
        }
        scatterChart.Y.Add(labelData);
        return scatterChart;
    }

    private Chart? DetectSideBar(List<FieldValues> table)
    {
        if (table.Count == 1)
        {
            Chart sideBarChart = new Chart();
            sideBarChart.ChartType = ChartType.SideBar;
            Dictionary<object, int> cnt =  new Dictionary<object, int>();
            Index(cnt, table[0]);
            var showData = cnt.OrderByDescending(kv => kv.Value).Take(20).ToList();
            sideBarChart.X = showData.Select(kv => kv.Key).ToList();
            LabelData labelData = new LabelData();
            labelData.Label = $"Count of \"{table[0].Field.Name}\"";
            labelData.Data = showData.Select(kv => kv.Value).Cast<object>().ToList();
            sideBarChart.Y.Add(labelData);
            return sideBarChart;
        }

        if (table.Count == 2 &&
            table[1].Field.Categories.Contains(Category.Numeric))
        {
            Chart sideBarChart = new Chart();
            sideBarChart.ChartType = ChartType.SideBar;
            Dictionary<object, (double sum, int cnt)> avg = new Dictionary<object, (double sum, int cnt)>();
            for (int i = 0; i < table[0].Values.Count; i++)
            {
                var val = table[1].Values[i].ValueCasted;
                if (val == null) continue;
                if (table[0].Values[i].ValueCasted == null) continue;
                if (table[0].Field.Type == typeof(List<String>))
                {
                    foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                    {
                        if (f == null) continue; 
                        avg[f] = avg.TryGetValue(f, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1);
                    }
                }
                else
                {
                    var key = table[0].Values[i].ValueCasted;
                    if (key == null) continue;
                    if (avg.TryGetValue(key, out var sc)) avg[key] = (sc.sum + Convert.ToDouble(val), sc.cnt + 1);
                    else avg[key] = (Convert.ToDouble(val), 1);
                }
            }
            
            var data = avg.Select(kv => new { Key = kv.Key, Val = kv.Value.sum / (1.0 * kv.Value.cnt) })
                .OrderByDescending(x => x.Val)
                .Take(20)
                .ToList();
            sideBarChart.X = data.Select(kv =>  kv.Key).ToList();
            LabelData labelData = new LabelData();
            labelData.Label = $"Mean of \"{table[1].Field.Name}\" by \"{table[0].Field.Name}\"";
            labelData.Data = data.Select(kv => kv.Val).Cast<object>().ToList();
            sideBarChart.Y.Add(labelData);
            return sideBarChart;
        }
        return null;
    }
    
    private (double med, double IQR) GetScaleInfo(List<Value> values)
    {
        List<double> valueList =  new List<double>();
        foreach (Value value in values)
        {
            if (value.ValueCasted == null) continue;
            valueList.Add(Convert.ToDouble(value.ValueCasted));
        }
        valueList.Sort();
        double q1 = valueList[Math.Max((valueList.Count + 1) / 4 - 1, 0)];
        double q3 = valueList[valueList.Count - 1 - (valueList.Count + 1) / 4];
        double iqr = q3 - q1;
        return (valueList.Count % 2 != 0 ? valueList[valueList.Count / 2] : (valueList[valueList.Count / 2] + valueList[valueList.Count / 2 - 1]) / 2,
                iqr);
    }

    private Chart DetectMatrix(List<FieldValues> table)
    {
        Chart Matrix = new Chart();
        Matrix.ChartType = ChartType.Matrix;
        
        Dictionary<(object x, object y), double> dict2C = new();
        // Setup, Count
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;

            if (table[0].Field.Type == typeof(List<string>))
            {
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                        {
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                            else dict2C[(f, f1)]++;
                        }
                    }
                    else
                    {
                        var f1 = table[1].Values[i].ValueCasted;
                        if (f1 is null) continue;
                        if (!dict2C.TryGetValue((f, f1), out var cnt)) dict2C[(f, f1)] = 1;
                        else dict2C[(f, f1)]++;
                    }
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (table[1].Field.Categories.Contains(Category.Categorical))
                {
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        if (table[1].Values[i].ValueCasted == null) continue;
                        foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                        {
                            if (f1 is null) continue;
                            if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                            else dict2C[(key, f1)]++;
                        }
                    }
                    else
                    {
                        var f1 = table[1].Values[i].ValueCasted;
                        if (f1 is null) continue;
                        if (!dict2C.TryGetValue((key, f1), out var cnt)) dict2C[(key, f1)] = 1;
                        else dict2C[(key, f1)]++;
                    }
                }
            }
        }
        
        var xs = dict2C.Keys
            .Select(k => k.x)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var ys = dict2C.Keys
            .Select(k => k.y)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        Matrix.X.AddRange(xs);

        foreach (var yk in ys)
        {
            LabelData ykbyx = new();
            ykbyx.Label = yk.ToString();
            foreach (var xk in xs)
            {
                ykbyx.Data.Add(dict2C.GetValueOrDefault((xk, yk), 0));
            }
            Matrix.Y.Add(ykbyx);
        }
        
        return Matrix;
    }

    private Chart? DetectMixedBarChart(List<FieldValues> table)
    {
        Chart mixedBarChart = new Chart();
        mixedBarChart.ChartType = ChartType.MixedBar;
        Dictionary<object, (double sum, int cnt)> avg = new Dictionary<object, (double sum, int cnt)>();
        Dictionary<object, HashSet<object>> cntIdentifier = new Dictionary<object, HashSet<object>>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var val = table[1].Values[i].ValueCasted;
            var val2 = table[2].Values[i].ValueCasted;
            if (val == null || val2 == null) continue;
            if (table[0].Values[i].ValueCasted == null) continue;
            if (table[0].Field.Type == typeof(List<String>))
            {
                foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue; 
                    avg[f] = avg.TryGetValue(f, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1);
                    if (!cntIdentifier.TryGetValue(f, out var resId)) 
                        cntIdentifier[f] = resId = new HashSet<object>();
                    resId.Add(val2);
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (key == null) continue;
                if (avg.TryGetValue(key, out var sc)) avg[key] = (sc.sum + Convert.ToDouble(val), sc.cnt + 1);
                else avg[key] = (Convert.ToDouble(val), 1);
                if (!cntIdentifier.TryGetValue(key, out var resId)) 
                    cntIdentifier[key] = resId = new HashSet<object>();
                resId.Add(val2);
            }
        }
            
        var data = avg
            .Where(kv => kv.Value.cnt > 0)
            .Select(kv => new { 
                Key = kv.Key, 
                Val = kv.Value.sum / (1.0 * kv.Value.cnt), 
                distinctIdentifier = cntIdentifier.TryGetValue(kv.Key, out var res) ? res.Count : 0})
            .ToList();

        double meanVal = data.Select(kv => kv.Val).Average();
        double sdVal = double.Sqrt(data.Select(kv => (kv.Val - meanVal) * (kv.Val - meanVal)).Average());
        
        double meanCnt = data.Select(kv => kv.distinctIdentifier).Average();
        double sdCnt = double.Sqrt(data.Select(kv => (kv.distinctIdentifier - meanCnt) * (kv.distinctIdentifier - meanCnt)).Average());

        data = data
            .OrderByDescending(kv =>
                (sdCnt == 0 ? 0 : (kv.distinctIdentifier - meanCnt) / sdCnt) +
                (sdVal == 0 ? 0 : (kv.Val - meanVal) / sdVal))
            .Take(20)
            .ToList();
        mixedBarChart.X = data.Select(kv =>  kv.Key).ToList();
        LabelData labelDataY = new LabelData();
        labelDataY.Label = $"Mean of \"{table[1].Field.Name}\" by \"{table[0].Field.Name}\"";
        labelDataY.Data = data.Select(kv => kv.Val).Cast<object>().ToList();
        labelDataY.ChartType = ChartType.Bar;
        mixedBarChart.Y.Add(labelDataY);
        LabelData labelDataZ = new LabelData();
        labelDataZ.Label = $"Count of \"{table[2].Field.Name}\" by \"{table[0].Field.Name}\"";
        labelDataZ.Data = data.Select(kv => kv.distinctIdentifier).Cast<object>().ToList();
        labelDataZ.ChartType = ChartType.Bar;
        mixedBarChart.Z.Add(labelDataZ);
        return mixedBarChart;
    }

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default)
    {
        Table tempTable = _lqDbService.GetTable(csvfileid, fieldindexes);
        int minimumRelationship = 0;
        List<Chart?> res = new();
        
        List<FieldValues> identifier = new List<FieldValues>();
        List<FieldValues> categorical = new List<FieldValues>();
        List<List<FieldValues>> numeric = new List<List<FieldValues>>();
        List<FieldValues> numericList = new List<FieldValues>();
        List<(double med, double IQR)> scaleInfo = new List<(double med, double IQR)>(); 
        foreach (FieldValues fv in tempTable.Columns)
        {
            if (fv.Field.Categories.Contains(Category.Identifier))
                identifier.Add(fv);
            if (fv.Field.Categories.Contains(Category.Categorical))
                categorical.Add(fv);
            if (fv.Field.Categories.Contains(Category.Numeric))
            {
                numericList.Add(fv);
                (double med, double IQR) fvInfo = GetScaleInfo(fv.Values);
                bool found = false;
                for (int i = 0; i < numeric.Count; i++)
                {
                    (double med, double IQR) curInfo = scaleInfo[i];
                    if (Math.Abs(curInfo.med - fvInfo.med) / Math.Max(fvInfo.IQR, curInfo.IQR) <= 1 &&
                        Math.Max(curInfo.IQR, fvInfo.IQR) / Math.Min(curInfo.IQR, fvInfo.IQR) <= 2)
                    {
                        found = true;
                        numeric[i].Add(fv);
                        List<Value> newList = new List<Value>();
                        foreach (FieldValues fvi in numeric[i])
                        foreach(Value val in fvi.Values) newList.Add(val);
                        scaleInfo[i] = GetScaleInfo(newList);
                    }
                }
                if (!found)
                {
                    numeric.Add(new List<FieldValues> {fv});
                    scaleInfo.Add(fvInfo);
                }
            }
        }

        // X, Y, Z
        foreach (var num in numeric)
        {
            foreach (var fv in num)
            {
                System.Console.Write($"{fv.Field.Name},");
            }

            System.Console.WriteLine();
        }
        
        for(int categoricalIndex = 0; categoricalIndex < categorical.Count; categoricalIndex++)
        {
            // X Categorical, Y Categorical, Z Categorical
            // X Categorical, Y Categorical, Z Numeric
            for(int categoricalSubIndex = categoricalIndex + 1; categoricalSubIndex < categorical.Count; categoricalSubIndex++)
            {
                foreach (List<FieldValues> sameNumerics in numeric)
                {
                    foreach (FieldValues numericFv in sameNumerics)
                    {
                        if (numericFv != categorical[categoricalIndex] && numericFv != categorical[categoricalSubIndex])
                        {
                            if (categorical[categoricalIndex].Field.Type != typeof(DateTime) && 
                                categorical[categoricalSubIndex].Field.Type != typeof(DateTime) &&
                                CountDistinctValues(categorical[categoricalIndex]) <= 20 &&
                                CountDistinctValues(categorical[categoricalSubIndex]) <= 20)
                                res.Add(DetectMatrix(new List<FieldValues>
                                {
                                    categorical[categoricalIndex],
                                    categorical[categoricalSubIndex],
                                    numericFv,
                                }));
                            else
                                res.Add(DetectLineChart(new List<FieldValues>
                                {
                                    (categorical[categoricalIndex].Field.Type == typeof(DateTime) ? categorical[categoricalIndex] : categorical[categoricalSubIndex]),
                                    (categorical[categoricalIndex].Field.Type == typeof(DateTime) ? categorical[categoricalSubIndex] : categorical[categoricalIndex]),
                                    numericFv,
                                }));
                        }   
                    }
                }
            }
            // X Categorical, Y Numeric, Z Identifier
            foreach (FieldValues numericFv in numericList)
            {
                if (categorical[categoricalIndex] == numericFv) continue;
                    foreach (FieldValues identifierFv in identifier)
                    {
                        if (identifierFv == numericFv || identifierFv == categorical[categoricalIndex]) continue;
                        res.Add(DetectMixedBarChart(new List<FieldValues>
                        {
                            categorical[categoricalIndex],
                            numericFv,
                            identifierFv
                        }));
                    }
            }
        }
        // X, Y
        for(int categoricalIndex = 0; categoricalIndex < categorical.Count; categoricalIndex++)
        {
            // X Categorical, Y Categorical
            for(int categoricalSubIndex = categoricalIndex + 1; categoricalSubIndex < categorical.Count; categoricalSubIndex++)
            {
                if (categorical[categoricalIndex].Field.Type == typeof(DateTime))
                {
                    if (categorical[categoricalSubIndex].Field.Type != typeof(DateTime))
                        res.Add(DetectLineChart(new List<FieldValues>
                        {
                            categorical[categoricalIndex],
                            categorical[categoricalSubIndex]
                        }));
                }
                else
                {
                    if (categorical[categoricalSubIndex].Field.Type == typeof(DateTime))
                        res.Add(DetectLineChart(new List<FieldValues>
                        {
                            categorical[categoricalSubIndex],
                            categorical[categoricalIndex]
                        }));
                    else
                    {
                        if (CountDistinctValues(categorical[categoricalIndex]) <= 20 &&
                            CountDistinctValues(categorical[categoricalSubIndex]) <= 20)
                            res.Add(DetectMatrix(new List<FieldValues>
                            {
                                categorical[categoricalIndex],
                                categorical[categoricalSubIndex]
                            }));
                        else
                            res.Add(DetectLineChart(new List<FieldValues>
                            {
                                categorical[categoricalIndex],
                                categorical[categoricalSubIndex]
                            }));
                    }
                }
            }
            // X Categorical, Y Numeric
            foreach (List<FieldValues> lfv in numeric)
            {
                foreach (FieldValues numericfv in lfv) 
                {
                    if (numericfv != categorical[categoricalIndex])
                    {
                        if (categorical[categoricalIndex].Field.Type == typeof(DateTime) || categorical[categoricalIndex].Field.Categories.Contains(Category.Numeric))
                        {
                            res.Add(DetectLineChart(new List<FieldValues>
                            {
                                categorical[categoricalIndex],
                                numericfv
                            }));
                        }
                        else
                        {
                            res.Add(CountDistinctValues(categorical[categoricalIndex]) <= 20
                                ? DetectBarChart(new List<FieldValues>{categorical[categoricalIndex], numericfv})
                                : DetectSideBar(new List<FieldValues>{categorical[categoricalIndex], numericfv})
                            );
                        }
                    }
                }
            }
            // X Categorical, Y list<Numeric>
            foreach (List<FieldValues> lfv in numeric)
            {
                List<FieldValues> sameScale = new List<FieldValues>();
                foreach (FieldValues nfv in lfv) if (nfv != categorical[categoricalIndex]) sameScale.Add(nfv);
                if (sameScale.Count == 1) continue;
                sameScale.Insert(0, categorical[categoricalIndex]);
                res.Add(DetectBarChart(sameScale));
            }
        }
        // X numeric, Y numeric
            for (int numericIndex = 0; numericIndex < numericList.Count; numericIndex++)
            {
                for (int numericSubIndex = numericIndex + 1; numericSubIndex < numericList.Count; numericSubIndex++)
                {
                    res.Add(DetectScatter(new List<FieldValues>
                    {
                        numericList[numericIndex],
                        numericList[numericSubIndex]
                    }));
                }
            }
        // X identifier, Y numeric
        foreach (FieldValues identifierFv in identifier)
        foreach (FieldValues numericFv in numericList) if (identifierFv != numericFv) {
            res.Add(DetectSideBar(new List<FieldValues>()
            {
                identifierFv,
                numericFv
            }));
        }

        // X categorical
        foreach (FieldValues categoricalFv in categorical)
        {
            if (CountDistinctValues(categoricalFv) <= 20)
                res.Add(DetectPieChart(new List<FieldValues> { categoricalFv }));
            else res.Add(DetectSideBar(new List<FieldValues> { categoricalFv }));
        }
        // X identifier
        foreach (FieldValues identifierFv in identifier)
        {
            res.Add(DetectSideBar(new List<FieldValues> { identifierFv }));
        }
        // X numeric
        foreach (List<FieldValues> lfv in numeric)
        {
            foreach (FieldValues numericfv in lfv) res.Add(DetectHistogram(new List<FieldValues> {numericfv}));
        }
        
        List<Chart> noNullRes = new List<Chart>();
        foreach (Chart? chart in res) 
        {
            if (chart != null) noNullRes.Add(chart);
        }
        return noNullRes;
    }
}