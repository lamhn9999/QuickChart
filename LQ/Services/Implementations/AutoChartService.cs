using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Table = LQ.Models.TableModels.Table;

namespace LQ.Services.Implementations;

public class AutoChartService : IAutoChartService
{
    private readonly ILQDbService _lqDbService;
    public AutoChartService(ILQDbService lqDbService)
    {
        _lqDbService = lqDbService;
    }

    // ─── existing helpers ─────────────────────────────────────────────────────

    private int CountDistinctValues(FieldValues fv)
    {
        var cnt = new Dictionary<object, int>();
        Index(cnt, fv);
        return cnt.Count;
    }

    private void Index(Dictionary<object, int> cnt, FieldValues table)
    {
        foreach (var value in table.Values)
        {
            if (value.ValueCasted == null) continue;
            if (table.Field.Type == typeof(List<string>))
            {
                foreach (var f in (List<string>)value.ValueCasted)
                {
                    if (f == null) continue;
                    cnt[f] = cnt.TryGetValue(f, out var n) ? n + 1 : 1;
                }
            }
            else
                cnt[value.ValueCasted] = cnt.TryGetValue(value.ValueCasted, out var n) ? n + 1 : 1;
        }
    }

    private DateTime ConvertToDateTime(long val) => new DateTime(val, DateTimeKind.Utc).Date;
    private string GetYear(object val) => ConvertToDateTime((long)val).Year.ToString();

    // ─── NEW: time-granularity helpers ────────────────────────────────────────

    private enum TimeGranularity { Hour, Day, Week, Month, Quarter, Year }

    private TimeGranularity DetectGranularity(List<Value> values)
    {
        var dates = values.Where(v => v.ValueCasted != null)
                          .Select(v => (DateTime)v.ValueCasted!).ToList();
        if (dates.Count < 2) return TimeGranularity.Day;
        var span = dates.Max() - dates.Min();
        if (span.TotalDays <= 2)    return TimeGranularity.Hour;
        if (span.TotalDays <= 60)   return TimeGranularity.Day;
        if (span.TotalDays <= 183)  return TimeGranularity.Week;
        if (span.TotalDays <= 730)  return TimeGranularity.Month;
        if (span.TotalDays <= 1825) return TimeGranularity.Quarter;
        return TimeGranularity.Year;
    }

    private static DateTime TruncToGranularity(DateTime dt, TimeGranularity g) => g switch
    {
        TimeGranularity.Hour    => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0),
        TimeGranularity.Day     => dt.Date,
        TimeGranularity.Week    => dt.Date.AddDays(-(int)dt.DayOfWeek),
        TimeGranularity.Month   => new DateTime(dt.Year, dt.Month, 1),
        TimeGranularity.Quarter => new DateTime(dt.Year, ((dt.Month - 1) / 3) * 3 + 1, 1),
        TimeGranularity.Year    => new DateTime(dt.Year, 1, 1),
        _                       => dt.Date,
    };

    private static string FmtDate(DateTime dt, TimeGranularity g) => g switch
    {
        TimeGranularity.Hour    => dt.ToString("MM/dd HH:mm"),
        TimeGranularity.Day     => dt.ToString("MMM d"),
        TimeGranularity.Week    => $"Wk {dt:MMM d}",
        TimeGranularity.Month   => dt.ToString("MMM yyyy"),
        TimeGranularity.Quarter => $"Q{(dt.Month - 1) / 3 + 1} {dt.Year}",
        TimeGranularity.Year    => dt.Year.ToString(),
        _                       => dt.ToString("MMM d"),
    };

    // ─── NEW: stats helpers ───────────────────────────────────────────────────

    /// <summary>Ordinary least squares. Returns (slope, intercept, R²).</summary>
    private static (double slope, double intercept, double r2) LinearRegression(List<(double x, double y)> pts)
    {
        int n = pts.Count;
        if (n < 2) return (0, 0, 0);
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        foreach (var (x, y) in pts) { sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x; }
        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-10) return (0, sumY / n, 0);
        double slope     = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;
        double meanY     = sumY / n;
        double ssTot     = pts.Sum(p => (p.y - meanY) * (p.y - meanY));
        double ssRes     = pts.Sum(p => { var pred = slope * p.x + intercept; return (p.y - pred) * (p.y - pred); });
        double r2        = ssTot > 1e-10 ? Math.Max(0, 1 - ssRes / ssTot) : 1;
        return (slope, intercept, r2);
    }

    /// <summary>Centered moving average. Edges use narrower windows.</summary>
    private static List<double?> MovingAverage(List<double> vals, int window)
    {
        var result = new List<double?>(vals.Count);
        int half = window / 2;
        for (int i = 0; i < vals.Count; i++)
        {
            int start = Math.Max(0, i - half);
            int end   = Math.Min(vals.Count - 1, i + half);
            if (end - start + 1 < Math.Max(2, window / 2)) { result.Add(null); continue; }
            result.Add(vals.Skip(start).Take(end - start + 1).Average());
        }
        return result;
    }

    /// <summary>Pearson correlation coefficient for two aligned double lists.</summary>
    private static double PearsonR(List<double> xs, List<double> ys)
    {
        if (xs.Count != ys.Count || xs.Count < 2) return 0;
        double mx = xs.Average(), my = ys.Average();
        double num    = xs.Zip(ys, (x, y) => (x - mx) * (y - my)).Sum();
        double denomX = Math.Sqrt(xs.Sum(x => (x - mx) * (x - mx)));
        double denomY = Math.Sqrt(ys.Sum(y => (y - my) * (y - my)));
        if (denomX * denomY < 1e-10) return 0;
        return Math.Max(-1, Math.Min(1, num / (denomX * denomY)));
    }

    // ─── EXISTING detectors (unchanged) ──────────────────────────────────────

    private Chart? DetectBarChart(List<FieldValues> table)
    {
        if (table.Count == 1) return null;
        var barChart = new Chart { ChartType = ChartType.Bar };
        var dict2C = new Dictionary<(object x, object y), int>();
        var prep   = new Dictionary<object, (double sum, int cnt)>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            if (table[0].Field.Type == typeof(List<string>))
            {
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (!prep.ContainsKey(f)) prep[f] = (0, 0);
                    if (table[1].Field.Categories.Contains(Category.Categorical))
                    {
                        if (table[1].Field.Type == typeof(List<string>))
                        {
                            foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                            { if (f1 is null) continue; dict2C.TryGetValue((f, f1), out var c0); dict2C[(f, f1)] = c0 + 1; }
                        }
                        else { var f1 = table[1].Values[i].ValueCasted; if (f1 != null) { dict2C.TryGetValue((f, f1), out var c0); dict2C[(f, f1)] = c0 + 1; } }
                    }
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (!prep.ContainsKey(key)) prep[key] = (0, 0);
                if (table[1].Field.Categories.Contains(Category.Categorical))
                {
                    if (table[1].Field.Type == typeof(List<string>))
                    {
                        if (table[1].Values[i].ValueCasted == null) continue;
                        foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted)
                        { if (f1 is null) continue; dict2C.TryGetValue((key, f1), out var c0); dict2C[(key, f1)] = c0 + 1; }
                    }
                    else { var f1 = table[1].Values[i].ValueCasted; if (f1 != null) { dict2C.TryGetValue((key, f1), out var c0); dict2C[(key, f1)] = c0 + 1; } }
                }
            }
        }

        barChart.X = prep.OrderBy(x => x.Key).Select(kv => kv.Key).ToList();

        for (int j = 1; j < table.Count; j++)
        {
            if (!table[j].Field.Categories.Contains(Category.Numeric)) continue;
            var avg = new Dictionary<object, (double sum, int cnt)>(prep);
            for (int i = 0; i < table[0].Values.Count; i++)
            {
                var val = table[j].Values[i].ValueCasted;
                if (table[0].Values[i].ValueCasted == null || val == null) continue;
                if (table[0].Field.Type == typeof(List<string>))
                    foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                    { if (f != null) avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1); }
                else { var k = table[0].Values[i].ValueCasted; if (k != null) avg[k] = (avg[k].sum + Convert.ToDouble(val), avg[k].cnt + 1); }
            }
            var data = avg.OrderBy(kv => kv.Key)
                         .Select(kv => new { Key = kv.Key, Val = kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / kv.Value.cnt }).ToList();
            barChart.Y.Add(new LabelData
            {
                Label = $"Mean of \"{table[j].Field.Name}\" by \"{table[0].Field.Name}\"",
                Data  = data.Select(kv => (object)kv.Val).ToList()
            });
        }

        var xs2 = dict2C.Keys.Select(k => k.x).Distinct().OrderBy(x => x).ToList();
        var ys2 = dict2C.Keys.Select(k => k.y).Distinct().OrderBy(y => y).ToList();
        foreach (var yk in ys2)
            barChart.Y.Add(new LabelData
            {
                Label = $"Count of \"{yk}\" by \"{table[0].Field.Name}\"",
                Data  = xs2.Select(xk => (object)dict2C.GetValueOrDefault((xk, yk), 0)).ToList()
            });

        return barChart;
    }

    private Chart? DetectPieChart(List<FieldValues> table)
    {
        var c = new Chart { ChartType = ChartType.Pie };
        var cnt = new Dictionary<object, int>(); Index(cnt, table[0]);
        c.X = cnt.Select(kv => kv.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Count of \"{table[0].Field.Name}\"", Data = cnt.Select(kv => (object)kv.Value).ToList() });
        return c;
    }

    private Chart? DetectLineChart(List<FieldValues> table)
    {
        var lineChart = new Chart { ChartType = ChartType.Line };
        var dict2C    = new Dictionary<(object x, object y), int>();
        var prep      = new Dictionary<object, (double sum, int cnt)>();
        var ysPos     = new Dictionary<object, HashSet<int>>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            void Track(object f, object? f1)
            {
                if (!prep.ContainsKey(f)) prep[f] = (0, 0);
                if (f1 == null) return;
                dict2C.TryGetValue((f, f1), out var c0); dict2C[(f, f1)] = c0 + 1;
                if (!ysPos.TryGetValue(f1, out var set)) { set = new HashSet<int>(); ysPos[f1] = set; }
                set.Add(i);
            }

            if (table[0].Field.Type == typeof(List<string>))
            {
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (table[1].Field.Categories.Contains(Category.Categorical))
                    {
                        if (table[1].Field.Type == typeof(List<string>))
                        { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Track(f, f1); }
                        else Track(f, table[1].Values[i].ValueCasted);
                    }
                    else if (!prep.ContainsKey(f)) prep[f] = (0, 0);
                }
            }
            else
            {
                var key = table[0].Values[i].ValueCasted;
                if (table[1].Field.Categories.Contains(Category.Categorical))
                {
                    if (table[1].Field.Type == typeof(List<string>))
                    { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Track(key, f1); }
                    else Track(key, table[1].Values[i].ValueCasted);
                }
                else if (!prep.ContainsKey(key)) prep[key] = (0, 0);
            }
        }

        lineChart.X = prep.OrderBy(x => x.Key).Select(kv => kv.Key).ToList();

        if (table[1].Field.Categories.Contains(Category.Numeric))
        {
            for (int j = 1; j < table.Count; j++)
            {
                if (!table[j].Field.Categories.Contains(Category.Numeric)) continue;
                var avg = new Dictionary<object, (double sum, int cnt)>(prep);
                for (int i = 0; i < table[0].Values.Count; i++)
                {
                    var val = table[j].Values[i].ValueCasted;
                    if (table[0].Values[i].ValueCasted == null || val == null) continue;
                    if (table[0].Field.Type == typeof(List<string>))
                        foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                        { if (f != null) avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1); }
                    else { var k = table[0].Values[i].ValueCasted; if (k != null) avg[k] = (avg[k].sum + Convert.ToDouble(val), avg[k].cnt + 1); }
                }
                lineChart.Y.Add(new LabelData
                {
                    Label = $"Mean of \"{table[j].Field.Name}\" by \"{table[0].Field.Name}\"",
                    Data  = avg.OrderBy(kv => kv.Key).Select(kv => (object)(kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / kv.Value.cnt)).ToList()
                });
            }
            return lineChart;
        }

        var xs = dict2C.Keys.Select(k => k.x).Distinct().OrderBy(x => x).ToList();
        var ys = dict2C.Keys.Select(k => k.y).Distinct().OrderBy(y => y).ToList();

        foreach (var yk in ys)
        {
            if (!ysPos.TryGetValue(yk, out var posSet)) continue;
            for (int j = 2; j < table.Count; j++)
            {
                var avg = new Dictionary<object, (double sum, int cnt)>(prep);
                foreach (int i in posSet)
                {
                    var val = table[j].Values[i].ValueCasted;
                    if (table[0].Values[i].ValueCasted == null || val == null) continue;
                    if (table[0].Field.Type == typeof(List<string>))
                        foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                        { if (f != null) avg[f] = (avg[f].sum + Convert.ToDouble(val), avg[f].cnt + 1); }
                    else { var k = table[0].Values[i].ValueCasted; if (k != null) avg[k] = (avg[k].sum + Convert.ToDouble(val), avg[k].cnt + 1); }
                }
                lineChart.Z.Add(new LabelData
                {
                    Label     = $"Mean of \"{table[j].Field.Name}\" where {table[1].Field.Name} = {yk} by \"{table[0].Field.Name}\"",
                    ChartType = ChartType.Line,
                    Data      = avg.OrderBy(kv => kv.Key).Select(kv => (object)(kv.Value.cnt == 0 ? 0.0 : kv.Value.sum / kv.Value.cnt)).ToList()
                });
            }
        }
        if (lineChart.Z.Count != 0) lineChart.ChartType = ChartType.Mixed;
        foreach (var yk in ys)
            lineChart.Y.Add(new LabelData
            {
                Label     = $"Count of \"{yk}\" by \"{table[0].Field.Name}\"",
                ChartType = ChartType.Bar,
                Data      = xs.Select(xk => (object)dict2C.GetValueOrDefault((xk, yk), 0)).ToList()
            });

        return lineChart;
    }

    private Chart? DetectPolarAreaChart(List<FieldValues> table)
    {
        var c = new Chart { ChartType = ChartType.PolarArea };
        var cnt = new Dictionary<object, int>(); Index(cnt, table[0]);
        c.X = cnt.Select(kv => kv.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Count of \"{table[0].Field.Name}\"", Data = cnt.Select(kv => (object)kv.Value).ToList() });
        return c;
    }

    private Chart? DetectHistogram(List<FieldValues> table)
    {
        if (table[0].Field.Categories.Contains(Category.Numeric))
        {
            var valueList = table[0].Values.Where(v => v.ValueCasted != null).Select(v => Convert.ToDouble(v.ValueCasted)).OrderBy(x => x).ToList();
            if (valueList.Count < 2) return null;
            double gap = (valueList.Last() - valueList.First()) / ((int)Math.Sqrt(valueList.Count) + 1);
            if (gap == 0) gap = 1;
            var c = new Chart { ChartType = ChartType.Histogram };
            var listXVal = new List<double>();
            double xv = valueList.First();
            while (c.X.Count == 0 || xv < valueList.Last())
            {
                listXVal.Add(xv);
                c.X.Add(table[0].Field.Type == typeof(DateTime)
                    ? $"[{ConvertToDateTime((long)xv)},{ConvertToDateTime((long)(xv + gap))})"
                    : $"[{xv}, {Math.Round(xv + gap, 2)})");
                xv = Math.Round(xv + gap, 2);
            }
            listXVal.Add(xv);
            var yVal = new int[c.X.Count];
            foreach (double v in valueList)
                for (int i = 0; i < c.X.Count; i++)
                    if (v >= listXVal[i] && v < listXVal[i + 1]) { yVal[i]++; break; }
            var ld = new LabelData { Label = $"Distribution of \"{table[0].Field.Name}\"" };
            foreach (int y in yVal) ld.Data.Add(y);
            c.Y.Add(ld);
            return c;
        }
        if (table[0].Field.Categories.Contains(Category.Categorical))
        {
            var c = new Chart { ChartType = ChartType.Histogram };
            var cnt = new Dictionary<object, int>(); Index(cnt, table[0]);
            c.X = cnt.Select(kv => kv.Key).ToList();
            if (c.X.Count > 20) return null;
            c.Y.Add(new LabelData { Label = $"Count of \"{table[0].Field.Name}\"", Data = cnt.Select(kv => (object)kv.Value).ToList() });
            return c;
        }
        return null;
    }

    private Chart? DetectScatter(List<FieldValues> table)
    {
        var c = new Chart { ChartType = ChartType.Scatter };
        if (table[0].Field.Categories.Contains(Category.Categorical) && table.Count == 3)
        {
            var listData = new Dictionary<object, LabelData>();
            for (int i = 0; i < table[0].Values.Count; i++)
            {
                var val1 = table[1].Values[i].ValueCasted; var val2 = table[2].Values[i].ValueCasted;
                if (table[0].Values[i].ValueCasted == null || val1 == null || val2 == null) continue;
                void Add(object f) { if (!listData.TryGetValue(f, out var ld)) { ld = new LabelData { Label = f.ToString() }; listData[f] = ld; } listData[f].Data.Add(new { x = val1, y = val2 }); }
                if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)table[0].Values[i].ValueCasted) { if (f != null) Add(f); }
                else { var f = table[0].Values[i].ValueCasted; if (f != null) Add(f); }
            }
            foreach (var item in listData) c.Y.Add(item.Value);
        }
        else
        {
            var ld = new LabelData { Label = $"Relationship between \"{table[0].Field.Name}\" and \"{table[1].Field.Name}\"" };
            for (int i = 0; i < table[0].Values.Count; i++)
            {
                if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null) continue;
                ld.Data.Add(new { x = table[0].Values[i].ValueCasted, y = table[1].Values[i].ValueCasted });
            }
            c.Y.Add(ld);
        }
        return c;
    }

    private Chart? DetectSideBar(List<FieldValues> table)
    {
        if (table.Count == 1)
        {
            var c = new Chart { ChartType = ChartType.SideBar };
            var cnt = new Dictionary<object, int>(); Index(cnt, table[0]);
            var show = cnt.OrderByDescending(kv => kv.Value).Take(20).ToList();
            c.X = show.Select(kv => kv.Key).ToList();
            c.Y.Add(new LabelData { Label = $"Count of \"{table[0].Field.Name}\"", Data = show.Select(kv => (object)kv.Value).ToList() });
            return c;
        }
        if (table.Count == 2 && table[1].Field.Categories.Contains(Category.Numeric))
        {
            var c = new Chart { ChartType = ChartType.SideBar };
            var avg = new Dictionary<object, (double sum, int cnt)>();
            for (int i = 0; i < table[0].Values.Count; i++)
            {
                var val = table[1].Values[i].ValueCasted; if (val == null || table[0].Values[i].ValueCasted == null) continue;
                if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)table[0].Values[i].ValueCasted)
                { if (f == null) continue; avg[f] = avg.TryGetValue(f, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1); }
                else { var k = table[0].Values[i].ValueCasted; if (k != null) avg[k] = avg.TryGetValue(k, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1); }
            }
            var data = avg.Select(kv => new { Key = kv.Key, Val = kv.Value.sum / kv.Value.cnt }).OrderByDescending(x => x.Val).Take(20).ToList();
            c.X = data.Select(kv => kv.Key).ToList();
            c.Y.Add(new LabelData { Label = $"Mean of \"{table[1].Field.Name}\" by \"{table[0].Field.Name}\"", Data = data.Select(kv => (object)kv.Val).ToList() });
            return c;
        }
        return null;
    }

    private (double med, double IQR) GetScaleInfo(List<Value> values)
    {
        var vl = values.Where(v => v.ValueCasted != null).Select(v => Convert.ToDouble(v.ValueCasted)).OrderBy(x => x).ToList();
        if (vl.Count == 0) return (0, 1);
        double q1 = vl[Math.Max((vl.Count + 1) / 4 - 1, 0)];
        double q3 = vl[vl.Count - 1 - (vl.Count + 1) / 4];
        return (vl.Count % 2 != 0 ? vl[vl.Count / 2] : (vl[vl.Count / 2] + vl[vl.Count / 2 - 1]) / 2, q3 - q1);
    }

    private Chart DetectMatrix(List<FieldValues> table)
    {
        var c = new Chart { ChartType = ChartType.Matrix };
        var d = new Dictionary<(object x, object y), double>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            void Inc(object a, object? b) { if (b == null) return; d.TryGetValue((a, b), out var v); d[(a, b)] = v + 1; }
            if (table[0].Field.Type == typeof(List<string>))
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (table[1].Field.Type == typeof(List<string>)) { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Inc(f, f1); }
                    else Inc(f, table[1].Values[i].ValueCasted);
                }
            else
            {
                var k = table[0].Values[i].ValueCasted;
                if (!table[1].Field.Categories.Contains(Category.Categorical)) continue;
                if (table[1].Field.Type == typeof(List<string>)) { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Inc(k, f1); }
                else Inc(k, table[1].Values[i].ValueCasted);
            }
        }
        var xs = d.Keys.Select(k => k.x).Distinct().OrderBy(x => x).ToList();
        var ys = d.Keys.Select(k => k.y).Distinct().OrderBy(y => y).ToList();
        foreach (var x in xs) c.X.Add(table[0].Field.Type == typeof(bool) ? $"{table[0].Field.Name} = {x}" : x.ToString());
        foreach (var yk in ys)
        {
            var row = new LabelData { Label = table[0].Field.Type == typeof(bool) ? $"{table[1].Field.Name} = {yk}" : yk.ToString() };
            foreach (var xk in xs) row.Data.Add(d.GetValueOrDefault((xk, yk), 0));
            c.Y.Add(row);
        }
        return c;
    }

    private Chart DetectMatrixMean(List<FieldValues> table)
    {
        var c = new Chart { ChartType = ChartType.Matrix };
        var d2 = new Dictionary<(object x, object y), double>();
        var d1 = new Dictionary<object, double>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            void Inc(object a, object? b)
            {
                if (b == null) return;
                d2.TryGetValue((a, b), out var v); d2[(a, b)] = v + 1;
                d1.TryGetValue(a, out var v2); d1[a] = v2 + 1;
            }
            if (table[0].Field.Type == typeof(List<string>))
                foreach (string? f in (List<string?>)table[0].Values[i].ValueCasted)
                {
                    if (f == null) continue;
                    if (table[1].Field.Type == typeof(List<string>)) { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Inc(f, f1); }
                    else Inc(f, table[1].Values[i].ValueCasted);
                }
            else
            {
                var k = table[0].Values[i].ValueCasted;
                if (!table[1].Field.Categories.Contains(Category.Categorical)) continue;
                if (table[1].Field.Type == typeof(List<string>)) { if (table[1].Values[i].ValueCasted != null) foreach (string? f1 in (List<string?>)table[1].Values[i].ValueCasted) Inc(k, f1); }
                else Inc(k, table[1].Values[i].ValueCasted);
            }
        }
        var xs = d2.Keys.Select(k => k.x).Distinct().OrderBy(x => x).ToList();
        var ys = d2.Keys.Select(k => k.y).Distinct().OrderBy(y => y).ToList();
        foreach (var x in xs) c.X.Add(table[0].Field.Type == typeof(bool) ? $"{table[0].Field.Name} = {x}" : x.ToString());
        foreach (var yk in ys)
        {
            var row = new LabelData { Label = table[0].Field.Type == typeof(bool) ? $"{table[1].Field.Name} = {yk}" : yk.ToString() };
            foreach (var xk in xs) row.Data.Add(d2.GetValueOrDefault((xk, yk), 0) / d1.GetValueOrDefault(xk, 1));
            c.Y.Add(row);
        }
        return c;
    }

    private Chart? DetectMixedBarChart(List<FieldValues> table)
    {
        var c    = new Chart { ChartType = ChartType.MixedBar };
        var avg  = new Dictionary<object, (double sum, int cnt)>();
        var cntId = new Dictionary<object, HashSet<object>>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var val = table[1].Values[i].ValueCasted; var val2 = table[2].Values[i].ValueCasted;
            if (val == null || val2 == null || table[0].Values[i].ValueCasted == null) continue;
            void Add(object f) {
                avg[f] = avg.TryGetValue(f, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1);
                if (!cntId.TryGetValue(f, out var s)) { s = new HashSet<object>(); cntId[f] = s; } s.Add(val2);
            }
            if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)table[0].Values[i].ValueCasted) { if (f != null) Add(f); }
            else { var k = table[0].Values[i].ValueCasted; if (k != null) Add(k); }
        }
        var data = avg.Where(kv => kv.Value.cnt > 0)
            .Select(kv => new { Key = kv.Key, Val = kv.Value.sum / kv.Value.cnt, Cnt = cntId.TryGetValue(kv.Key, out var s) ? s.Count : 0 }).ToList();
        double mV = data.Select(d => d.Val).Average(), sdV = Math.Sqrt(data.Select(d => (d.Val - mV) * (d.Val - mV)).Average());
        double mC = data.Select(d => (double)d.Cnt).Average(), sdC = Math.Sqrt(data.Select(d => (d.Cnt - mC) * (d.Cnt - mC)).Average());
        data = data.OrderByDescending(d => (sdC == 0 ? 0 : (d.Cnt - mC) / sdC) + (sdV == 0 ? 0 : (d.Val - mV) / sdV)).Take(20).ToList();
        c.X = data.Select(d => d.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{table[1].Field.Name}\" by \"{table[0].Field.Name}\"", ChartType = ChartType.Bar, Data = data.Select(d => (object)d.Val).ToList() });
        c.Z.Add(new LabelData { Label = $"Count of \"{table[2].Field.Name}\" by \"{table[0].Field.Name}\"", ChartType = ChartType.Bar, Data = data.Select(d => (object)d.Cnt).ToList() });
        return c;
    }

    private Chart? DetectMixedLineChart(List<FieldValues> table)
    {
        var c    = new Chart { ChartType = ChartType.MixedBar };
        var avg1 = new Dictionary<object, (double sum, int cnt)>();
        var avg2 = new Dictionary<object, (double sum, int cnt)>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var val = table[1].Values[i].ValueCasted; var val2 = table[2].Values[i].ValueCasted;
            if (val == null || val2 == null || table[0].Values[i].ValueCasted == null) continue;
            void Add(object f) {
                avg1[f] = avg1.TryGetValue(f, out var n)  ? (n.sum  + Convert.ToDouble(val),  n.cnt  + 1) : (Convert.ToDouble(val),  1);
                avg2[f] = avg2.TryGetValue(f, out var n2) ? (n2.sum + Convert.ToDouble(val2), n2.cnt + 1) : (Convert.ToDouble(val2), 1);
            }
            if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)table[0].Values[i].ValueCasted) { if (f != null) Add(f); }
            else { var k = table[0].Values[i].ValueCasted; if (k != null) Add(k); }
        }
        var d1 = avg1.Where(kv => kv.Value.cnt > 0).Select(kv => new { Key = kv.Key, Val = kv.Value.sum / kv.Value.cnt }).OrderBy(x => x.Key).ToList();
        var d2 = avg2.Where(kv => kv.Value.cnt > 0).Select(kv => new { Key = kv.Key, Val = kv.Value.sum / kv.Value.cnt }).OrderBy(x => x.Key).ToList();
        c.X = d1.Select(x => x.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{table[1].Field.Name}\" by \"{table[0].Field.Name}\"", ChartType = ChartType.Line, Data = d1.Select(x => (object)x.Val).ToList() });
        c.Z.Add(new LabelData { Label = $"Mean of \"{table[2].Field.Name}\" by \"{table[0].Field.Name}\"", ChartType = ChartType.Line, Data = d2.Select(x => (object)x.Val).ToList() });
        return c;
    }

    // ─── NEW detectors ────────────────────────────────────────────────────────

    /// <summary>Line chart binned by auto-detected granularity — replaces raw-datetime DetectLineChart for DateTime fields.</summary>
    private Chart? DetectTimeSeriesLine(List<FieldValues> table)
    {
        if (table.Count < 2 || table[0].Field.Type != typeof(DateTime)) return null;
        var numericFields = table.Skip(1).Where(f => f.Field.Categories.Contains(Category.Numeric)).ToList();
        if (numericFields.Count == 0) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, Dictionary<int, (double sum, int cnt)>>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            var dt = TruncToGranularity((DateTime)table[0].Values[i].ValueCasted!, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = new Dictionary<int, (double, int)>();
            for (int j = 0; j < numericFields.Count; j++)
            {
                var val = numericFields[j].Values[i].ValueCasted;
                if (val == null) continue;
                if (!buckets[dt].ContainsKey(j)) buckets[dt][j] = (0, 0);
                var cur = buckets[dt][j]; buckets[dt][j] = (cur.sum + Convert.ToDouble(val), cur.cnt + 1);
            }
        }
        if (buckets.Count < 2) return null;

        var chart       = new Chart { ChartType = ChartType.Line, Title = $"{numericFields[0].Field.Name} over time ({gran.ToString().ToLower()})" };
        var sortedDates = buckets.Keys.ToList();
        chart.X = sortedDates.Select(d => (object)FmtDate(d, gran)).ToList();

        for (int j = 0; j < numericFields.Count; j++)
        {
            var avgs = sortedDates.Select(d => buckets[d].TryGetValue(j, out var sc) && sc.cnt > 0 ? (object)Math.Round(sc.sum / sc.cnt, 4) : 0.0).ToList();
            chart.Y.Add(new LabelData { Label = $"Avg {numericFields[j].Field.Name}", Data = avgs });
        }
        return chart;
    }

    /// <summary>
    /// Enhanced time series: auto-bins dates to day/week/month/year, and overlays
    /// a centered moving average so trends emerge from noisy data immediately.
    /// </summary>
    private Chart? DetectTimeSeriesEnhanced(List<FieldValues> table)
    {
        if (table.Count < 2 || table[0].Field.Type != typeof(DateTime)) return null;
        var numericFields = table.Skip(1).Where(f => f.Field.Categories.Contains(Category.Numeric)).ToList();
        if (numericFields.Count == 0) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, Dictionary<int, (double sum, int cnt)>>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null) continue;
            var dt = TruncToGranularity((DateTime)table[0].Values[i].ValueCasted!, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = new Dictionary<int, (double, int)>();
            for (int j = 0; j < numericFields.Count; j++)
            {
                var val = numericFields[j].Values[i].ValueCasted;
                if (val == null) continue;
                if (!buckets[dt].ContainsKey(j)) buckets[dt][j] = (0, 0);
                var cur = buckets[dt][j]; buckets[dt][j] = (cur.sum + Convert.ToDouble(val), cur.cnt + 1);
            }
        }
        if (buckets.Count < 3) return null;

        var chart = new Chart { ChartType = ChartType.MixedBar, Title = $"{numericFields[0].Field.Name} over time ({gran.ToString().ToLower()})" };
        var sortedDates = buckets.Keys.ToList();
        chart.X = sortedDates.Select(d => (object)FmtDate(d, gran)).ToList();

        for (int j = 0; j < numericFields.Count; j++)
        {
            var avgs = sortedDates.Select(d => buckets[d].TryGetValue(j, out var sc) && sc.cnt > 0 ? sc.sum / sc.cnt : 0.0).ToList();
            chart.Y.Add(new LabelData { Label = $"Avg {numericFields[j].Field.Name}", ChartType = ChartType.Bar, Data = avgs.Cast<object>().ToList() });

            int window = Math.Clamp(sortedDates.Count / 6, 3, 14);
            if (sortedDates.Count >= window * 2)
            {
                var ma = MovingAverage(avgs, window);
                chart.Y.Add(new LabelData
                {
                    Label     = $"{window}-pt moving avg",
                    ChartType = ChartType.Line,
                    Data      = ma.Select(v => v.HasValue ? (object)Math.Round(v.Value, 4) : null!).ToList()
                });
            }
        }
        return chart;
    }

    /// <summary>Running cumulative total over time — reveals acceleration and plateaus.</summary>
    private Chart? DetectCumulativeSum(List<FieldValues> table)
    {
        if (table.Count < 2 || table[0].Field.Type != typeof(DateTime)) return null;
        if (!table[1].Field.Categories.Contains(Category.Numeric)) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, double>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null) continue;
            var dt  = TruncToGranularity((DateTime)table[0].Values[i].ValueCasted!, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = 0;
            buckets[dt] += Convert.ToDouble(table[1].Values[i].ValueCasted);
        }
        if (buckets.Count < 4) return null;

        var chart = new Chart { ChartType = ChartType.Line, Title = $"Cumulative {table[1].Field.Name}" };
        chart.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        double running = 0;
        chart.Y.Add(new LabelData
        {
            Label = $"Cumulative {table[1].Field.Name}",
            Data  = buckets.Values.Select(v => { running += v; return (object)Math.Round(running, 4); }).ToList()
        });
        return chart;
    }

    /// <summary>Period-over-period % change — instantly surfaces momentum shifts.</summary>
    private Chart? DetectGrowthRate(List<FieldValues> table)
    {
        if (table.Count < 2 || table[0].Field.Type != typeof(DateTime)) return null;
        if (!table[1].Field.Categories.Contains(Category.Numeric)) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, (double sum, int cnt)>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null) continue;
            var dt  = TruncToGranularity((DateTime)table[0].Values[i].ValueCasted!, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = (0, 0);
            var cur = buckets[dt]; buckets[dt] = (cur.sum + Convert.ToDouble(table[1].Values[i].ValueCasted), cur.cnt + 1);
        }
        if (buckets.Count < 4) return null;

        var periods = buckets.Select(kv => kv.Value.cnt > 0 ? kv.Value.sum / kv.Value.cnt : 0.0).ToList();
        var labels  = buckets.Keys.Select(d => FmtDate(d, gran)).ToList();
        double base0 = periods[0];

        var pctChange = new List<object>();
        for (int i = 1; i < periods.Count; i++)
        {
            double prev = periods[i - 1];
            pctChange.Add(Math.Abs(prev) < 1e-10 ? null! : (object)Math.Round((periods[i] - prev) / Math.Abs(prev) * 100, 2));
        }
        var cumChange = periods.Skip(1)
            .Select(v => Math.Abs(base0) < 1e-10 ? null! : (object)Math.Round((v - base0) / Math.Abs(base0) * 100, 2))
            .ToList();

        var chart = new Chart { ChartType = ChartType.MixedBar, Title = $"Growth rate: {table[1].Field.Name} ({gran.ToString().ToLower()} over {gran.ToString().ToLower()})" };
        chart.X = labels.Skip(1).Cast<object>().ToList();
        chart.Y.Add(new LabelData { Label = "% change (period)", ChartType = ChartType.Bar,  Data = pctChange });
        chart.Y.Add(new LabelData { Label = "Cumulative % from start", ChartType = ChartType.Line, Data = cumChange });
        return chart;
    }

    /// <summary>Scatter + OLS regression line with R² — see correlation at a glance.</summary>
    private Chart? DetectScatterRegression(List<FieldValues> table)
    {
        if (table.Count < 2) return null;
        if (!table[0].Field.Categories.Contains(Category.Numeric) || !table[1].Field.Categories.Contains(Category.Numeric)) return null;

        var pts = new List<(double x, double y)>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null) continue;
            pts.Add((Convert.ToDouble(table[0].Values[i].ValueCasted), Convert.ToDouble(table[1].Values[i].ValueCasted)));
        }
        if (pts.Count < 5) return null;

        var (slope, intercept, r2) = LinearRegression(pts);
        double minX = pts.Min(p => p.x), maxX = pts.Max(p => p.x);

        var chart = new Chart { ChartType = ChartType.Scatter, Title = $"{table[0].Field.Name} vs {table[1].Field.Name}  (R² = {r2:F2})" };
        var scatter = new LabelData { Label = $"{table[0].Field.Name} / {table[1].Field.Name}" };
        foreach (var p in pts) scatter.Data.Add(new { x = p.x, y = p.y });
        chart.Y.Add(scatter);

        // Label prefix "Trend:" → workspace.js applies dashed showLine style
        var lineDs = new LabelData { Label = $"Trend: y = {slope:F3}x + {intercept:F3}" };
        for (int k = 0; k <= 12; k++) { double xk = minX + (maxX - minX) * k / 12.0; lineDs.Data.Add(new { x = Math.Round(xk, 6), y = Math.Round(slope * xk + intercept, 6) }); }
        chart.Y.Add(lineDs);
        return chart;
    }

    /// <summary>Pareto bar + cumulative-% line — instant 80/20 view.</summary>
    private Chart? DetectParetoChart(List<FieldValues> table)
    {
        if (table.Count != 1 || !table[0].Field.Categories.Contains(Category.Categorical)) return null;
        var cnt = new Dictionary<object, int>(); Index(cnt, table[0]);
        if (cnt.Count < 3 || cnt.Count > 60) return null;

        var sorted = cnt.OrderByDescending(kv => kv.Value).ToList();
        double total = sorted.Sum(kv => kv.Value);

        var chart = new Chart { ChartType = ChartType.MixedBar, Title = $"Pareto: {table[0].Field.Name}" };
        chart.X = sorted.Select(kv => (object)kv.Key).ToList();
        chart.Y.Add(new LabelData { Label = $"Count of {table[0].Field.Name}", ChartType = ChartType.Bar, Data = sorted.Select(kv => (object)kv.Value).ToList() });

        double running = 0;
        var cumDs = new LabelData { Label = "Cumulative %", ChartType = ChartType.Line };
        foreach (var kv in sorted) { running += kv.Value; cumDs.Data.Add(Math.Round(running / total * 100, 1)); }
        chart.Z.Add(cumDs);
        return chart;
    }

    /// <summary>Count of records per time bucket — the right chart for a lone DateTime column.</summary>
    private Chart? DetectDateTimeFrequency(List<FieldValues> table)
    {
        if (table.Count != 1 || table[0].Field.Type != typeof(DateTime)) return null;
        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, int>();
        foreach (var v in table[0].Values)
        {
            if (v.ValueCasted == null) continue;
            var dt = TruncToGranularity((DateTime)v.ValueCasted!, gran);
            buckets[dt] = buckets.TryGetValue(dt, out var n) ? n + 1 : 1;
        }
        if (buckets.Count < 2) return null;
        var chart = new Chart { ChartType = ChartType.Bar, Title = $"{table[0].Field.Name} frequency ({gran.ToString().ToLower()})" };
        chart.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        chart.Y.Add(new LabelData { Label = $"Count of {table[0].Field.Name}", Data = buckets.Values.Select(v => (object)v).ToList() });
        return chart;
    }

    /// <summary>DateTime + categorical + numeric: bins dates by granularity, one line per category value showing mean(numeric).</summary>
    private Chart? DetectTimeSeriesCategoricalNumeric(List<FieldValues> table)
    {
        if (table.Count < 3 || table[0].Field.Type != typeof(DateTime)) return null;
        if (!table[1].Field.Categories.Contains(Category.Categorical) || table[1].Field.Type == typeof(DateTime)) return null;
        if (!table[2].Field.Categories.Contains(Category.Numeric)) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, Dictionary<string, (double sum, int cnt)>>();
        var cats    = new HashSet<string>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var dtVal  = table[0].Values[i].ValueCasted;
            var catVal = table[1].Values[i].ValueCasted;
            var numVal = table[2].Values[i].ValueCasted;
            if (dtVal == null || catVal == null || numVal == null) continue;

            var dt = TruncToGranularity((DateTime)dtVal, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = new Dictionary<string, (double, int)>();

            IEnumerable<string?> catValues = table[1].Field.Type == typeof(List<string>)
                ? (List<string>)catVal
                : new[] { catVal.ToString() };

            foreach (var cat in catValues)
            {
                if (cat == null) continue;
                cats.Add(cat);
                if (!buckets[dt].ContainsKey(cat)) buckets[dt][cat] = (0, 0);
                var cur = buckets[dt][cat];
                buckets[dt][cat] = (cur.sum + Convert.ToDouble(numVal), cur.cnt + 1);
            }
        }

        if (buckets.Count < 2) return null;
        var sortedCats = cats.OrderBy(c => c).ToList();
        if (sortedCats.Count > 20) return null;

        var sortedDates = buckets.Keys.ToList();
        var chart = new Chart
        {
            ChartType = ChartType.Line,
            Title     = $"Avg {table[2].Field.Name} by {table[1].Field.Name} over time ({gran.ToString().ToLower()})"
        };
        chart.X = sortedDates.Select(d => (object)FmtDate(d, gran)).ToList();
        foreach (var cat in sortedCats)
        {
            var data = sortedDates.Select(d =>
                buckets[d].TryGetValue(cat, out var sc) && sc.cnt > 0
                    ? (object)Math.Round(sc.sum / sc.cnt, 4)
                    : 0.0).ToList();
            chart.Y.Add(new LabelData { Label = cat, Data = data });
        }
        return chart;
    }

    /// <summary>DateTime + categorical string: bins dates by granularity, one line per category value.</summary>
    private Chart? DetectTimeSeriesCategorical(List<FieldValues> table)
    {
        if (table.Count < 2 || table[0].Field.Type != typeof(DateTime)) return null;
        if (!table[1].Field.Categories.Contains(Category.Categorical) || table[1].Field.Type == typeof(DateTime)) return null;

        var gran    = DetectGranularity(table[0].Values);
        var buckets = new SortedDictionary<DateTime, Dictionary<string, int>>();
        var cats    = new HashSet<string>();

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null) continue;
            var dt = TruncToGranularity((DateTime)table[0].Values[i].ValueCasted!, gran);
            if (!buckets.ContainsKey(dt)) buckets[dt] = new Dictionary<string, int>();
            IEnumerable<string?> catValues = table[1].Field.Type == typeof(List<string>)
                ? (List<string>)table[1].Values[i].ValueCasted!
                : new[] { table[1].Values[i].ValueCasted!.ToString() };
            foreach (var cat in catValues)
            {
                if (cat == null) continue;
                cats.Add(cat);
                buckets[dt][cat] = buckets[dt].TryGetValue(cat, out var n) ? n + 1 : 1;
            }
        }

        if (buckets.Count < 2) return null;
        var sortedCats = cats.OrderBy(c => c).ToList();
        if (sortedCats.Count > 20) return null;

        var sortedDates = buckets.Keys.ToList();
        var chart = new Chart
        {
            ChartType = ChartType.Line,
            Title     = $"{table[1].Field.Name} by {table[0].Field.Name} ({gran.ToString().ToLower()})"
        };
        chart.X = sortedDates.Select(d => (object)FmtDate(d, gran)).ToList();
        foreach (var cat in sortedCats)
            chart.Y.Add(new LabelData
            {
                Label = cat,
                Data  = sortedDates.Select(d => (object)buckets[d].GetValueOrDefault(cat, 0)).ToList()
            });
        return chart;
    }

    /// <summary>
    /// Pairwise Pearson R heatmap for all selected numerics.
    /// workspace.js detects "Correlation matrix" in the title and applies a
    /// diverging colour scale: teal (−1) → neutral (0) → coral (+1).
    /// </summary>
    private Chart? DetectCorrelationMatrix(List<FieldValues> fields)
    {
        if (fields.Count < 3) return null;
        int rows = fields[0].Values.Count;
        var data = fields.Select(f => f.Values.Select(v => v.ValueCasted != null ? (double?)Convert.ToDouble(v.ValueCasted) : null).ToList()).ToList();

        var chart = new Chart { ChartType = ChartType.Matrix, Title = "Correlation matrix" };
        var names = fields.Select(f => f.Field.Name).ToList();
        chart.X = names.Cast<object>().ToList();

        for (int i = 0; i < fields.Count; i++)
        {
            var row = new LabelData { Label = names[i] };
            for (int j = 0; j < fields.Count; j++)
            {
                var pX = new List<double>(); var pY = new List<double>();
                for (int r = 0; r < rows; r++) if (data[i][r].HasValue && data[j][r].HasValue) { pX.Add(data[i][r]!.Value); pY.Add(data[j][r]!.Value); }
                row.Data.Add(Math.Round(PearsonR(pX, pY), 2));
            }
            chart.Y.Add(row);
        }
        return chart;
    }

    /// <summary>
    /// Bubble chart: 3 numerics — X, Y, and bubble radius.
    /// Requires ChartType.Bubble = 10 and chartArray[10] = 'bubble' in workspace.js.
    /// </summary>
    private Chart? DetectBubbleChart(List<FieldValues> table)
    {
        if (table.Count < 3) return null;
        for (int k = 0; k < 3; k++) if (!table[k].Field.Categories.Contains(Category.Numeric)) return null;
        var rawR = table[2].Values.Where(v => v.ValueCasted != null).Select(v => Convert.ToDouble(v.ValueCasted)).ToList();
        if (rawR.Count < 3) return null;

        double minR = rawR.Min(), maxR = rawR.Max(), range = Math.Max(maxR - minR, 1e-10);
        var chart = new Chart { ChartType = (ChartType)10, Title = $"Bubble: {table[0].Field.Name} / {table[1].Field.Name} (size: {table[2].Field.Name})" };
        var ds = new LabelData { Label = $"{table[0].Field.Name} · {table[1].Field.Name} · {table[2].Field.Name}" };

        for (int i = 0; i < table[0].Values.Count; i++)
        {
            if (table[0].Values[i].ValueCasted == null || table[1].Values[i].ValueCasted == null || table[2].Values[i].ValueCasted == null) continue;
            double r = 1 + (Convert.ToDouble(table[2].Values[i].ValueCasted) - minR) / range * 29;
            ds.Data.Add(new { x = Math.Round(Convert.ToDouble(table[0].Values[i].ValueCasted), 6), y = Math.Round(Convert.ToDouble(table[1].Values[i].ValueCasted), 6), r = Math.Round(r, 2) });
        }
        chart.Y.Add(ds);
        return chart;
    }

    /// <summary>Bar sorted by descending mean value — top 25 only. "What matters most" view.</summary>
    private Chart? DetectRankedBar(List<FieldValues> table)
    {
        if (table.Count < 2 || !table[0].Field.Categories.Contains(Category.Categorical)) return null;
        if (!table[1].Field.Categories.Contains(Category.Numeric) || table[0].Field.Type == typeof(DateTime)) return null;

        var avg = new Dictionary<object, (double sum, int cnt)>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var key = table[0].Values[i].ValueCasted; var val = table[1].Values[i].ValueCasted;
            if (key == null || val == null) continue;
            void Add(object f) { avg[f] = avg.TryGetValue(f, out var n) ? (n.sum + Convert.ToDouble(val), n.cnt + 1) : (Convert.ToDouble(val), 1); }
            if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)key) { if (f != null) Add(f); }
            else Add(key);
        }
        if (avg.Count < 3) return null;

        var sorted = avg.Where(kv => kv.Value.cnt > 0).Select(kv => new { Key = kv.Key, Val = kv.Value.sum / kv.Value.cnt }).OrderByDescending(x => x.Val).Take(25).ToList();
        var chart = new Chart { ChartType = ChartType.Bar, Title = $"Ranked: {table[1].Field.Name} by {table[0].Field.Name}" };
        chart.X = sorted.Select(x => (object)x.Key).ToList();
        chart.Y.Add(new LabelData { Label = $"Mean {table[1].Field.Name}", Data = sorted.Select(x => (object)Math.Round(x.Val, 4)).ToList() });
        return chart;
    }

    /// <summary>
    /// Five-number summary scatter (Min, Q1, Median, Q3, Max) per category.
    /// No plugin required — workspace.js renders with cross/dash point styles.
    /// </summary>
    private Chart? DetectBoxPlot(List<FieldValues> table)
    {
        if (table.Count < 2 || !table[0].Field.Categories.Contains(Category.Categorical)) return null;
        if (!table[1].Field.Categories.Contains(Category.Numeric) || table[0].Field.Type == typeof(DateTime)) return null;

        var groups = new Dictionary<object, List<double>>();
        for (int i = 0; i < table[0].Values.Count; i++)
        {
            var key = table[0].Values[i].ValueCasted; var val = table[1].Values[i].ValueCasted;
            if (key == null || val == null) continue;
            void Add(object f) { if (!groups.TryGetValue(f, out var lst)) { lst = new List<double>(); groups[f] = lst; } lst.Add(Convert.ToDouble(val)); }
            if (table[0].Field.Type == typeof(List<string>)) foreach (var f in (List<string>)key) { if (f != null) Add(f); } else Add(key);
        }
        if (groups.Count < 2 || groups.Count > 30) return null;

        static double Q(List<double> s, double q) { double pos = q * (s.Count - 1); int lo = (int)pos, hi = Math.Min(lo + 1, s.Count - 1); return s[lo] + (pos - lo) * (s[hi] - s[lo]); }

        var cats = groups.Keys.OrderBy(k => k).ToList();

        // Build summary as named tuples — avoids Func<dynamic,double> DLR dispatch
        var sum = cats.Select(cat => {
            var s = groups[cat].OrderBy(v => v).ToList();
            return (
                Cat:    cat,
                Min:    s.First(),
                Q1:     Q(s, 0.25),
                Med:    Q(s, 0.5),
                Q3:     Q(s, 0.75),
                Max:    s.Last()
            );
        }).ToList();

        var chart = new Chart { ChartType = ChartType.Scatter, Title = $"Distribution: {table[1].Field.Name} by {table[0].Field.Name}" };
        chart.X = cats.Cast<object>().ToList();

        // Emit each statistic as a named dataset — workspace.js detects "Median" label at index 2
        void AddStat(string name, Func<(object Cat, double Min, double Q1, double Med, double Q3, double Max), double> get)
        {
            var ds = new LabelData { Label = name };
            foreach (var item in sum) ds.Data.Add(new { x = item.Cat.ToString(), y = Math.Round(get(item), 4) });
            chart.Y.Add(ds);
        }

        AddStat("Min",    s => s.Min);
        AddStat("Q1",     s => s.Q1);
        AddStat("Median", s => s.Med);
        AddStat("Q3",     s => s.Q3);
        AddStat("Max",    s => s.Max);

        return chart;
    }

    // ─── public API (unchanged) ───────────────────────────────────────────────

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default)
        => CreateChart(_lqDbService.GetTable(csvfileid, fieldindexes));

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters, CancellationToken cancellationToken = default)
        => CreateChart(_lqDbService.GetTableWithFilter(csvfileid, fieldindexes, filters));

    public async Task<List<Chart>> DetectChartAsync(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default)
        => CreateChart(await _lqDbService.GetTableAsync(csvfileid, fieldindexes));

    // ─── CreateChart — updated wiring ─────────────────────────────────────────

    private List<Chart> CreateChart(Table tempTable)
    {
        var res         = new List<Chart?>();
        var identifier  = new List<FieldValues>();
        var categorical = new List<FieldValues>();
        var numeric     = new List<List<FieldValues>>();
        var numericList = new List<FieldValues>();
        var scaleInfo   = new List<(double med, double IQR)>();

        foreach (var fv in tempTable.Columns)
        {
            if (fv.Field.Categories.Contains(Category.Identifier))  identifier.Add(fv);
            if (fv.Field.Categories.Contains(Category.Categorical))  categorical.Add(fv);
            if (fv.Field.Categories.Contains(Category.Numeric))
            {
                numericList.Add(fv);
                var fi = GetScaleInfo(fv.Values);
                bool found = false;
                for (int i = 0; i < numeric.Count; i++)
                {
                    var ci = scaleInfo[i];
                    double maxIQR = Math.Max(ci.IQR, fi.IQR);
                    double minIQR = Math.Min(ci.IQR, fi.IQR);
                    bool sameScale = maxIQR < 1e-10
                        ? Math.Abs(ci.med - fi.med) < 1e-10
                        : Math.Abs(ci.med - fi.med) / maxIQR <= 1 && maxIQR / minIQR <= 2;
                    if (sameScale)
                    {
                        found = true; numeric[i].Add(fv);
                        var nl = new List<Value>(); foreach (var fvi in numeric[i]) foreach (var v in fvi.Values) nl.Add(v);
                        scaleInfo[i] = GetScaleInfo(nl);
                    }
                }
                if (!found) { numeric.Add(new List<FieldValues> { fv }); scaleInfo.Add(fi); }
            }
        }

        var ChartIndex    = new Dictionary<List<FieldValues>, int>();
        int curChartIndex = 0;
        void Reg(List<FieldValues> ct) { if (!ChartIndex.ContainsKey(ct)) ChartIndex[ct] = curChartIndex++; }
        void Add(Chart? ch, List<FieldValues> ct) { if (ch != null) { ch.PanelIndex = ChartIndex.GetValueOrDefault(ct, -1); res.Add(ch); } }

        // ── 3-field: cat × cat × numeric ─────────────────────────────────────
        for (int ci = 0; ci < categorical.Count; ci++)
        {
            for (int cj = ci + 1; cj < categorical.Count; cj++)
            {
                foreach (var ng in numeric) foreach (var nf in ng)
                {
                    if (nf == categorical[ci] || nf == categorical[cj]) continue;
                    bool noDateTime = categorical[ci].Field.Type != typeof(DateTime) && categorical[cj].Field.Type != typeof(DateTime);
                    if (noDateTime && CountDistinctValues(categorical[ci]) <= 10 && CountDistinctValues(categorical[cj]) <= 10)
                    {
                        var ct = new List<FieldValues> { categorical[ci], categorical[cj], nf }; Reg(ct);
                        Add(DetectMatrix(ct), ct);
                    }
                    else
                    {
                        var ct = new List<FieldValues>
                        {
                            categorical[ci].Field.Type == typeof(DateTime) ? categorical[ci] : categorical[cj],
                            categorical[ci].Field.Type == typeof(DateTime) ? categorical[cj] : categorical[ci],
                            nf
                        }; Reg(ct);
                        if (ct[0].Field.Type == typeof(DateTime))
                            Add(DetectTimeSeriesCategoricalNumeric(ct), ct);
                        else
                            Add(DetectLineChart(ct), ct);
                        if (ct[0].Field.Type == typeof(DateTime)) Add(DetectTimeSeriesEnhanced(ct), ct);
                    }
                }
            }

            // ── 3-field: cat × numeric × identifier ──────────────────────────
            foreach (var nf in numericList) { if (categorical[ci] == nf) continue;
                foreach (var id in identifier) { if (id == nf || id == categorical[ci]) continue;
                    var ct = new List<FieldValues> { categorical[ci], nf, id }; Reg(ct); Add(DetectMixedBarChart(ct), ct); } }

            // ── 3-field: cat × numeric × numeric ─────────────────────────────
            for (int ni = 0; ni < numericList.Count; ni++) { if (categorical[ci] == numericList[ni]) continue;
                for (int nj = ni + 1; nj < numericList.Count; nj++) { if (categorical[ci] == numericList[nj]) continue;
                    var ct = new List<FieldValues> { categorical[ci], numericList[ni], numericList[nj] }; Reg(ct);
                    Add(DetectMixedLineChart(ct), ct); Add(DetectScatter(ct), ct); } }
        }

        // ── 2-field: cat × cat, cat × numeric ────────────────────────────────
        for (int ci = 0; ci < categorical.Count; ci++)
        {
            for (int cj = ci + 1; cj < categorical.Count; cj++)
            {
                if (categorical[ci].Field.Type == typeof(DateTime))
                {
                    if (categorical[cj].Field.Type != typeof(DateTime))
                    { var ct = new List<FieldValues> { categorical[ci], categorical[cj] }; Reg(ct); Add(DetectTimeSeriesCategorical(ct), ct); }
                }
                else if (categorical[cj].Field.Type == typeof(DateTime))
                { var ct = new List<FieldValues> { categorical[cj], categorical[ci] }; Reg(ct); Add(DetectTimeSeriesCategorical(ct), ct); }
                else if (CountDistinctValues(categorical[ci]) <= 20 && CountDistinctValues(categorical[cj]) <= 20)
                { var ct = new List<FieldValues> { categorical[cj], categorical[ci] }; Reg(ct); Add(DetectMatrix(ct), ct); Add(DetectMatrixMean(ct), ct); }
                else
                { var ct = new List<FieldValues> { categorical[ci], categorical[cj] }; Reg(ct); Add(DetectLineChart(ct), ct); }
            }

            foreach (var ng in numeric) foreach (var nf in ng)
            {
                if (nf == categorical[ci]) continue;
                var ct = new List<FieldValues> { categorical[ci], nf }; Reg(ct);

                if (categorical[ci].Field.Type == typeof(DateTime) || categorical[ci].Field.Categories.Contains(Category.Numeric))
                {
                    if (categorical[ci].Field.Type == typeof(DateTime))
                        Add(DetectTimeSeriesLine(ct), ct);    // Line chart with granularity
                    else
                        Add(DetectLineChart(ct), ct);         // Numeric categorical: plain line
                    Add(DetectTimeSeriesEnhanced(ct), ct);
                    Add(DetectCumulativeSum(ct), ct);
                    Add(DetectGrowthRate(ct), ct);
                }
                else
                {
                    Add(CountDistinctValues(categorical[ci]) <= 20 ? DetectBarChart(ct) : DetectSideBar(ct), ct);
                    Add(DetectRankedBar(ct), ct);              // NEW
                    Add(DetectBoxPlot(ct), ct);                // NEW
                }
            }

            foreach (var ng in numeric)
            {
                var ss = ng.Where(nf => nf != categorical[ci]).ToList();
                if (ss.Count <= 1) continue;
                ss.Insert(0, categorical[ci]); Reg(ss); Add(DetectBarChart(ss), ss);
            }
        }

        // ── 2-field: numeric × numeric ────────────────────────────────────────
        for (int ni = 0; ni < numericList.Count; ni++)
        for (int nj = ni + 1; nj < numericList.Count; nj++)
        {
            var ct = new List<FieldValues> { numericList[ni], numericList[nj] }; Reg(ct);
            Add(DetectScatter(ct), ct);
            Add(DetectScatterRegression(ct), ct);              // NEW
        }

        // ── identifier × numeric ──────────────────────────────────────────────
        foreach (var id in identifier) foreach (var nf in numericList) { if (id == nf) continue;
            var ct = new List<FieldValues> { id, nf }; Reg(ct); Add(DetectSideBar(ct), ct); }

        // ── single categorical ────────────────────────────────────────────────
        foreach (var cat in categorical)
        {
            var ct = new List<FieldValues> { cat }; Reg(ct);
            if (cat.Field.Type == typeof(DateTime))
            {
                Add(DetectDateTimeFrequency(ct), ct);
            }
            else
            {
                var r = CountDistinctValues(cat) <= 20 ? DetectPieChart(ct) : DetectSideBar(ct);
                if (r != null) { r.PanelIndex = ChartIndex[ct]; res.Add(r); }
                int d = CountDistinctValues(cat);
                if (d is >= 3 and <= 60) Add(DetectParetoChart(ct), ct);
            }
        }

        // ── single identifier ─────────────────────────────────────────────────
        foreach (var id in identifier)
        { var ct = new List<FieldValues> { id }; Reg(ct); Add(DetectSideBar(ct), ct); }

        // ── single numeric (histogram) ────────────────────────────────────────
        foreach (var ng in numeric) foreach (var nf in ng)
        {
            if (nf.Field.Categories.Contains(Category.Identifier)) continue;
            var ct = new List<FieldValues> { nf }; Reg(ct); Add(DetectHistogram(ct), ct);
        }

        // ── NEW: Correlation matrix (3+ numerics) ─────────────────────────────
        var pureNum = numericList.Where(f => !f.Field.Categories.Contains(Category.Identifier)).ToList();
        if (pureNum.Count >= 3) { var ct = new List<FieldValues>(pureNum); Reg(ct); Add(DetectCorrelationMatrix(pureNum), ct); }

        // ── NEW: Bubble chart (every triple of numerics) ──────────────────────
        if (pureNum.Count >= 3)
        {
            for (int bi = 0; bi < pureNum.Count; bi++)
            for (int bj = bi + 1; bj < pureNum.Count; bj++)
            for (int bk = bj + 1; bk < pureNum.Count; bk++)
            {
                var bubKey = new List<FieldValues> { pureNum[bi], pureNum[bj], pureNum[bk] };
                var scKey  = new List<FieldValues> { pureNum[bi], pureNum[bj] };
                if (!ChartIndex.ContainsKey(bubKey)) ChartIndex[bubKey] = ChartIndex.GetValueOrDefault(scKey, curChartIndex++);
                Add(DetectBubbleChart(bubKey), bubKey);
            }
        }

        return res.Where(c => c != null).Select(c => c!).ToList();
    }
}