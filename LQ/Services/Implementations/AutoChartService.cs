using LQ.Data.Repositories;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Interfaces;
using Table = LQ.Models.TableModels.Table;

namespace LQ.Services.Implementations;

public class AutoChartService : IAutoChartService
{
    private readonly TableRepositoryAbstract _repo;

    private const int ScatterSampleSize     = 5_000;
    private const int CorrelationSampleSize = 50_000;

    public AutoChartService(TableRepositoryAbstract repo)
    {
        _repo = repo;
    }

    // ─── Formatting helpers ───────────────────────────────────────────────────

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

    // ─── Stats helpers ────────────────────────────────────────────────────────

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

    // ─── Public API ───────────────────────────────────────────────────────────

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default)
        => DetectChartAsync(csvfileid, fieldindexes, cancellationToken).GetAwaiter().GetResult();

    public async Task<List<Chart>> DetectChartAsync(int csvfileid, List<int> fieldindexes, CancellationToken cancellationToken = default)
        => await CreateChartAsync(csvfileid, fieldindexes);

    public List<Chart> DetectChart(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters, CancellationToken cancellationToken = default)
        => DetectChartWithFiltersAsync(csvfileid, fieldindexes, filters, cancellationToken).GetAwaiter().GetResult();

    private async Task<List<Chart>> DetectChartWithFiltersAsync(int csvfileid, List<int> fieldindexes, List<ValueFilterDTO> filters, CancellationToken cancellationToken = default)
        => await CreateChartAsync(csvfileid, fieldindexes, filters);

    // ═══════════════════════════════════════════════════════════════════════════
    // SQL-aggregate async path
    // ═══════════════════════════════════════════════════════════════════════════

    private static TimeGranularity DetectGranularity(DateTime min, DateTime max)
    {
        var span = max - min;
        if (span.TotalDays <= 2)    return TimeGranularity.Hour;
        if (span.TotalDays <= 60)   return TimeGranularity.Day;
        if (span.TotalDays <= 183)  return TimeGranularity.Week;
        if (span.TotalDays <= 730)  return TimeGranularity.Month;
        if (span.TotalDays <= 1825) return TimeGranularity.Quarter;
        return TimeGranularity.Year;
    }

    private async Task<TimeGranularity> GetGranularityAsync(Field dateField, int[]? rowFilter = null)
    {
        var (min, max) = await _repo.GetDateRange(dateField, rowFilter);
        return DetectGranularity(min, max);
    }

    // ── Count-based ───────────────────────────────────────────────────────────

    private async Task<Chart?> DetectPieChartAsync(Field groupField, int[]? rowFilter = null)
    {
        var rows = await _repo.CountByGroup(groupField, rowFilter);
        if (rows.Count == 0) return null;
        var c = new Chart { ChartType = ChartType.Pie };
        c.X = rows.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Count of \"{groupField.Name}\"", Data = rows.Select(r => (object)r.Count).ToList() });
        return c;
    }

    private async Task<Chart?> DetectPolarAreaAsync(Field groupField, int[]? rowFilter = null)
    {
        var c = await DetectPieChartAsync(groupField, rowFilter);
        if (c != null) c.ChartType = ChartType.PolarArea;
        return c;
    }

    private async Task<Chart?> DetectSideBarAsync(Field groupField, int[]? rowFilter = null)
    {
        var rows = await _repo.CountByGroup(groupField, rowFilter);
        if (rows.Count == 0) return null;
        var top = rows.OrderByDescending(r => r.Count).Take(20).ToList();
        var c = new Chart { ChartType = ChartType.SideBar };
        c.X = top.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Count of \"{groupField.Name}\"", Data = top.Select(r => (object)r.Count).ToList() });
        return c;
    }

    private async Task<Chart?> DetectSideBarAsync(Field catField, Field numericField, int[]? rowFilter = null)
    {
        var rows = await _repo.AverageByGroup(numericField, catField, rowFilter);
        if (rows.Count == 0) return null;
        var top = rows.OrderByDescending(r => r.Average).Take(20).ToList();
        var c = new Chart { ChartType = ChartType.SideBar };
        c.X = top.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{numericField.Name}\" by \"{catField.Name}\"", Data = top.Select(r => (object)r.Average).ToList() });
        return c;
    }

    private async Task<Chart?> DetectParetoChartAsync(Field catField, int[]? rowFilter = null)
    {
        var rows = await _repo.CountByGroup(catField, rowFilter);
        if (rows.Count < 3 || rows.Count > 60) return null;
        var sorted = rows.OrderByDescending(r => r.Count).ToList();
        double total = sorted.Sum(r => r.Count);
        var c = new Chart { ChartType = ChartType.MixedBar, Title = $"Pareto: {catField.Name}" };
        c.X = sorted.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Count of {catField.Name}", ChartType = ChartType.Bar, Data = sorted.Select(r => (object)r.Count).ToList() });
        double running = 0;
        var cumDs = new LabelData { Label = "Cumulative %", ChartType = ChartType.Line };
        foreach (var r in sorted) { running += r.Count; cumDs.Data.Add(Math.Round(running / total * 100, 1)); }
        c.Z.Add(cumDs);
        return c;
    }

    // ── Average-based ─────────────────────────────────────────────────────────

    private async Task<Chart?> DetectBarChartAsync(Field catField, Field numericField, int[]? rowFilter = null)
    {
        var rows = await _repo.AverageByGroup(numericField, catField, rowFilter);
        if (rows.Count == 0) return null;
        var ordered = rows.OrderBy(r => r.Key.ToString()).ToList();
        var c = new Chart { ChartType = ChartType.Bar };
        c.X = ordered.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{numericField.Name}\" by \"{catField.Name}\"", Data = ordered.Select(r => (object)Math.Round(r.Average, 4)).ToList() });
        return c;
    }

    private async Task<Chart?> DetectGroupedBarAsync(Field numericField, Field catFieldA, Field catFieldB, int[]? rowFilter = null)
    {
        var rows = await _repo.AverageByGroup(numericField, catFieldA, catFieldB, rowFilter);
        if (rows.Count == 0) return null;
        var xs = rows.Select(r => r.KeyA).Distinct().OrderBy(k => k.ToString()).ToList();
        var ys = rows.Select(r => r.KeyB).Distinct().OrderBy(k => k.ToString()).ToList();
        var lookup = rows.ToDictionary(r => (r.KeyA.ToString()!, r.KeyB.ToString()!), r => r.Average);
        var c = new Chart { ChartType = ChartType.Bar };
        c.X = xs.ToList();
        foreach (var yk in ys)
            c.Y.Add(new LabelData
            {
                Label = $"Mean of \"{numericField.Name}\" where \"{catFieldB.Name}\" = \"{yk}\"",
                Data  = xs.Select(xk => (object)Math.Round(lookup.GetValueOrDefault((xk.ToString()!, yk.ToString()!), 0.0), 4)).ToList()
            });
        return c;
    }

    private async Task<Chart?> DetectRankedBarAsync(Field catField, Field numericField, int[]? rowFilter = null)
    {
        var rows = await _repo.AverageByGroup(numericField, catField, rowFilter);
        if (rows.Count < 3) return null;
        var top = rows.OrderByDescending(r => r.Average).Take(25).ToList();
        var c = new Chart { ChartType = ChartType.Bar, Title = $"Ranked: {numericField.Name} by {catField.Name}" };
        c.X = top.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean {numericField.Name}", Data = top.Select(r => (object)Math.Round(r.Average, 4)).ToList() });
        return c;
    }

    private async Task<Chart?> DetectMixedLineChartAsync(Field catField, Field numericField1, Field numericField2, int[]? rowFilter = null)
    {
        var d1 = await _repo.AverageByGroup(numericField1, catField, rowFilter);
        var d2 = await _repo.AverageByGroup(numericField2, catField, rowFilter);
        if (d1.Count == 0) return null;
        var ordered1 = d1.OrderBy(r => r.Key.ToString()).ToList();
        var ordered2 = d2.ToDictionary(r => r.Key.ToString()!, r => r.Average);
        var c = new Chart { ChartType = ChartType.MixedBar };
        c.X = ordered1.Select(r => r.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{numericField1.Name}\" by \"{catField.Name}\"", ChartType = ChartType.Line, Data = ordered1.Select(r => (object)Math.Round(r.Average, 4)).ToList() });
        c.Z.Add(new LabelData { Label = $"Mean of \"{numericField2.Name}\" by \"{catField.Name}\"", ChartType = ChartType.Line, Data = ordered1.Select(r => (object)Math.Round(ordered2.GetValueOrDefault(r.Key.ToString()!, 0.0), 4)).ToList() });
        return c;
    }

    private async Task<Chart?> DetectMixedBarChartAsync(Field catField, Field numericField, Field identifierField, int[]? rowFilter = null)
    {
        var avgs  = await _repo.AverageByGroup(numericField, catField, rowFilter);
        var cdist = await _repo.CountDistinctByGroup(identifierField, catField, rowFilter);
        if (avgs.Count == 0) return null;
        var avgMap = avgs.ToDictionary(r => r.Key.ToString()!, r => r.Average);
        var cntMap = cdist.ToDictionary(r => r.Key.ToString()!, r => r.CountDistinct);
        var keys   = avgMap.Keys.ToList();
        double mV = avgMap.Values.Average(), sdV = Math.Sqrt(avgMap.Values.Select(v => (v - mV) * (v - mV)).Average());
        double mC = cntMap.Values.Select(v => (double)v).Average(), sdC = Math.Sqrt(cntMap.Values.Select(v => ((double)v - mC) * ((double)v - mC)).Average());
        var top = keys
            .Select(k => new { Key = k, Val = avgMap[k], Cnt = cntMap.GetValueOrDefault(k, 0) })
            .OrderByDescending(x => (sdC == 0 ? 0 : (x.Cnt - mC) / sdC) + (sdV == 0 ? 0 : (x.Val - mV) / sdV))
            .Take(20).ToList();
        var c = new Chart { ChartType = ChartType.MixedBar };
        c.X = top.Select(x => (object)x.Key).ToList();
        c.Y.Add(new LabelData { Label = $"Mean of \"{numericField.Name}\" by \"{catField.Name}\"",        ChartType = ChartType.Bar, Data = top.Select(x => (object)Math.Round(x.Val, 4)).ToList() });
        c.Z.Add(new LabelData { Label = $"Count of \"{identifierField.Name}\" by \"{catField.Name}\"", ChartType = ChartType.Bar, Data = top.Select(x => (object)x.Cnt).ToList() });
        return c;
    }

    // ── Matrix-based ──────────────────────────────────────────────────────────

    private async Task<Chart?> DetectMatrixAsync(Field catFieldA, Field catFieldB, int[]? rowFilter = null)
    {
        var rows = await _repo.CountByGroup(catFieldA, catFieldB, rowFilter);
        if (rows.Count == 0) return null;
        var xs = rows.Select(r => r.KeyA).Distinct().OrderBy(k => k.ToString()).ToList();
        var ys = rows.Select(r => r.KeyB).Distinct().OrderBy(k => k.ToString()).ToList();
        var lookup = rows.ToDictionary(r => (r.KeyA.ToString()!, r.KeyB.ToString()!), r => r.Count);
        var c = new Chart { ChartType = ChartType.Matrix };
        c.X = xs.Cast<object>().ToList();
        foreach (var yk in ys)
        {
            var row = new LabelData { Label = yk.ToString()! };
            foreach (var xk in xs) row.Data.Add(lookup.GetValueOrDefault((xk.ToString()!, yk.ToString()!), 0));
            c.Y.Add(row);
        }
        return c;
    }

    private async Task<Chart?> DetectMatrixMeanAsync(Field catFieldA, Field catFieldB, int[]? rowFilter = null)
    {
        var pairs  = await _repo.CountByGroup(catFieldA, catFieldB, rowFilter);
        var totals = await _repo.CountByGroup(catFieldA, rowFilter);
        if (pairs.Count == 0) return null;
        var xs     = pairs.Select(r => r.KeyA).Distinct().OrderBy(k => k.ToString()).ToList();
        var ys     = pairs.Select(r => r.KeyB).Distinct().OrderBy(k => k.ToString()).ToList();
        var pairLookup  = pairs.ToDictionary(r  => (r.KeyA.ToString()!, r.KeyB.ToString()!), r  => (double)r.Count);
        var totalLookup = totals.ToDictionary(r => r.Key.ToString()!, r => (double)r.Count);
        var c = new Chart { ChartType = ChartType.Matrix };
        c.X = xs.Cast<object>().ToList();
        foreach (var yk in ys)
        {
            var row = new LabelData { Label = yk.ToString()! };
            foreach (var xk in xs)
            {
                double num = pairLookup.GetValueOrDefault((xk.ToString()!, yk.ToString()!), 0);
                double den = totalLookup.GetValueOrDefault(xk.ToString()!, 1);
                row.Data.Add(Math.Round(num / den, 4));
            }
            c.Y.Add(row);
        }
        return c;
    }

    // ── Statistical ───────────────────────────────────────────────────────────

    private async Task<Chart?> DetectBoxPlotAsync(Field catField, Field numericField, int[]? rowFilter = null)
    {
        var rows = await _repo.FiveNumberSummaryByGroup(numericField, catField, rowFilter);
        if (rows.Count < 2 || rows.Count > 30) return null;
        var sorted = rows.OrderBy(r => r.Key.ToString()).ToList();
        var c = new Chart { ChartType = ChartType.Scatter, Title = $"Distribution: {numericField.Name} by {catField.Name}" };
        c.X = sorted.Select(r => r.Key).ToList();
        foreach (var (name, get) in new (string, Func<(object Key, double Min, double Q1, double Median, double Q3, double Max), double>)[]
        {
            ("Min",    r => r.Min),
            ("Q1",     r => r.Q1),
            ("Median", r => r.Median),
            ("Q3",     r => r.Q3),
            ("Max",    r => r.Max),
        })
        {
            var ds = new LabelData { Label = name };
            foreach (var r in sorted) ds.Data.Add(new { x = r.Key.ToString(), y = Math.Round(get(r), 4) });
            c.Y.Add(ds);
        }
        return c;
    }

    private async Task<Chart?> DetectHistogramAsync(Field field, int[]? rowFilter = null)
    {
        if (field.Categories.Contains(Category.Numeric))
        {
            var (min, max) = await _repo.MinMax(field, rowFilter);
            if (max - min < 1e-12) return null;
            int numBuckets = 40;
            double gap = (max - min) / numBuckets;
            var buckets = await _repo.GetHistogramBuckets(field, min, max, numBuckets, rowFilter);
            var c = new Chart { ChartType = ChartType.Histogram };
            for (int i = 1; i <= numBuckets; i++)
            {
                double lo = min + (i - 1) * gap;
                double hi = min + i * gap;
                c.X.Add($"[{Math.Round(lo, 4)}, {Math.Round(hi, 4)})");
            }
            var ld = new LabelData { Label = $"Distribution of \"{field.Name}\"" };
            for (int i = 1; i <= numBuckets; i++) ld.Data.Add(buckets.GetValueOrDefault(i, 0));
            c.Y.Add(ld);
            return c;
        }
        if (field.Categories.Contains(Category.Categorical))
        {
            var rows = await _repo.CountByGroup(field, rowFilter);
            if (rows.Count > 20) return null;
            var c = new Chart { ChartType = ChartType.Histogram };
            c.X = rows.Select(r => r.Key).ToList();
            c.Y.Add(new LabelData { Label = $"Count of \"{field.Name}\"", Data = rows.Select(r => (object)r.Count).ToList() });
            return c;
        }
        return null;
    }

    // ── Time-series ───────────────────────────────────────────────────────────

    private async Task<Chart?> DetectDateTimeFrequencyAsync(Field dateField, int[]? rowFilter = null)
    {
        var gran    = await GetGranularityAsync(dateField, rowFilter);
        var buckets = await _repo.CountByTimeBucket(dateField, gran, rowFilter);
        if (buckets.Count < 2) return null;
        var c = new Chart { ChartType = ChartType.Bar, Title = $"{dateField.Name} frequency ({gran.ToString().ToLower()})" };
        c.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        c.Y.Add(new LabelData { Label = $"Count of {dateField.Name}", Data = buckets.Values.Select(v => (object)v).ToList() });
        return c;
    }

    private async Task<Chart?> DetectTimeSeriesLineAsync(Field dateField, IReadOnlyList<Field> numericFields, int[]? rowFilter = null)
    {
        if (numericFields.Count == 0) return null;
        var gran = await GetGranularityAsync(dateField, rowFilter);
        var series = new SortedDictionary<DateTime, double>[numericFields.Count];
        for (int j = 0; j < numericFields.Count; j++)
            series[j] = await _repo.AverageByTimeBucket(numericFields[j], dateField, gran, rowFilter);
        var allDates = series.SelectMany(s => s.Keys).Distinct().OrderBy(d => d).ToList();
        if (allDates.Count < 2) return null;
        var c = new Chart { ChartType = ChartType.Line, Title = $"{numericFields[0].Name} over time ({gran.ToString().ToLower()})" };
        c.X = allDates.Select(d => (object)FmtDate(d, gran)).ToList();
        for (int j = 0; j < numericFields.Count; j++)
            c.Y.Add(new LabelData { Label = $"Avg {numericFields[j].Name}", Data = allDates.Select(d => (object)Math.Round(series[j].GetValueOrDefault(d, 0.0), 4)).ToList() });
        return c;
    }

    private async Task<Chart?> DetectTimeSeriesEnhancedAsync(Field dateField, IReadOnlyList<Field> numericFields, int[]? rowFilter = null)
    {
        if (numericFields.Count == 0) return null;
        var gran = await GetGranularityAsync(dateField, rowFilter);
        var series = new SortedDictionary<DateTime, double>[numericFields.Count];
        for (int j = 0; j < numericFields.Count; j++)
            series[j] = await _repo.AverageByTimeBucket(numericFields[j], dateField, gran, rowFilter);
        var allDates = series.SelectMany(s => s.Keys).Distinct().OrderBy(d => d).ToList();
        if (allDates.Count < 3) return null;
        var c = new Chart { ChartType = ChartType.MixedBar, Title = $"{numericFields[0].Name} over time ({gran.ToString().ToLower()})" };
        c.X = allDates.Select(d => (object)FmtDate(d, gran)).ToList();
        for (int j = 0; j < numericFields.Count; j++)
        {
            var avgs = allDates.Select(d => series[j].GetValueOrDefault(d, 0.0)).ToList();
            c.Y.Add(new LabelData { Label = $"Avg {numericFields[j].Name}", ChartType = ChartType.Bar, Data = avgs.Cast<object>().ToList() });
            int window = Math.Clamp(allDates.Count / 6, 3, 14);
            if (allDates.Count >= window * 2)
            {
                var ma = MovingAverage(avgs, window);
                c.Y.Add(new LabelData { Label = $"{window}-pt moving avg", ChartType = ChartType.Line, Data = ma.Select(v => v.HasValue ? (object)Math.Round(v.Value, 4) : null!).ToList() });
            }
        }
        return c;
    }

    private async Task<Chart?> DetectCumulativeSumAsync(Field dateField, Field numericField, int[]? rowFilter = null)
    {
        var gran    = await GetGranularityAsync(dateField, rowFilter);
        var buckets = await _repo.SumByTimeBucket(numericField, dateField, gran, rowFilter);
        if (buckets.Count < 4) return null;
        var c = new Chart { ChartType = ChartType.Line, Title = $"Cumulative {numericField.Name}" };
        c.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        double running = 0;
        c.Y.Add(new LabelData { Label = $"Cumulative {numericField.Name}", Data = buckets.Values.Select(v => { running += v; return (object)Math.Round(running, 4); }).ToList() });
        return c;
    }

    private async Task<Chart?> DetectGrowthRateAsync(Field dateField, Field numericField, int[]? rowFilter = null)
    {
        var gran    = await GetGranularityAsync(dateField, rowFilter);
        var buckets = await _repo.AverageByTimeBucket(numericField, dateField, gran, rowFilter);
        if (buckets.Count < 4) return null;
        var periods = buckets.Values.ToList();
        var labels  = buckets.Keys.Select(d => FmtDate(d, gran)).ToList();
        double base0 = periods[0];
        var pctChange = new List<object>();
        for (int i = 1; i < periods.Count; i++) { double prev = periods[i - 1]; pctChange.Add(Math.Abs(prev) < 1e-10 ? null! : (object)Math.Round((periods[i] - prev) / Math.Abs(prev) * 100, 2)); }
        var cumChange = periods.Skip(1).Select(v => Math.Abs(base0) < 1e-10 ? null! : (object)Math.Round((v - base0) / Math.Abs(base0) * 100, 2)).ToList();
        var c = new Chart { ChartType = ChartType.MixedBar, Title = $"Growth rate: {numericField.Name} ({gran.ToString().ToLower()} over {gran.ToString().ToLower()})" };
        c.X = labels.Skip(1).Cast<object>().ToList();
        c.Y.Add(new LabelData { Label = "% change (period)",      ChartType = ChartType.Bar,  Data = pctChange });
        c.Y.Add(new LabelData { Label = "Cumulative % from start", ChartType = ChartType.Line, Data = cumChange });
        return c;
    }

    private async Task<Chart?> DetectTimeSeriesCategoricalAsync(Field dateField, Field catField, int[]? rowFilter = null)
    {
        var gran    = await GetGranularityAsync(dateField, rowFilter);
        var buckets = await _repo.CountByTimeBucketAndCategory(dateField, catField, gran, rowFilter);
        if (buckets.Count < 2) return null;
        var cats = buckets.Values.SelectMany(d => d.Keys).Distinct().OrderBy(c => c).ToList();
        if (cats.Count > 20) return null;
        var c = new Chart { ChartType = ChartType.Line, Title = $"{catField.Name} by {dateField.Name} ({gran.ToString().ToLower()})" };
        c.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        foreach (var cat in cats)
            c.Y.Add(new LabelData { Label = cat, Data = buckets.Values.Select(d => (object)d.GetValueOrDefault(cat, 0)).ToList() });
        return c;
    }

    private async Task<Chart?> DetectTimeSeriesCategoricalNumericAsync(Field numericField, Field dateField, Field catField, int[]? rowFilter = null)
    {
        var gran    = await GetGranularityAsync(dateField, rowFilter);
        var buckets = await _repo.AverageByTimeBucketAndCategory(numericField, dateField, catField, gran, rowFilter);
        if (buckets.Count < 2) return null;
        var cats = buckets.Values.SelectMany(d => d.Keys).Distinct().OrderBy(c => c).ToList();
        if (cats.Count > 20) return null;
        var c = new Chart { ChartType = ChartType.Line, Title = $"Avg {numericField.Name} by {catField.Name} over time ({gran.ToString().ToLower()})" };
        c.X = buckets.Keys.Select(d => (object)FmtDate(d, gran)).ToList();
        foreach (var cat in cats)
            c.Y.Add(new LabelData { Label = cat, Data = buckets.Values.Select(d => (object)Math.Round(d.GetValueOrDefault(cat, 0.0), 4)).ToList() });
        return c;
    }

    // ─── Async CreateChart ────────────────────────────────────────────────────

    private async Task<List<Chart>> CreateChartAsync(int csvFileId, List<int> fieldIndexes, List<ValueFilterDTO>? filters = null)
    {
        var csvFile = await _repo.GetFile(csvFileId);
        var fields  = await _repo.GetFields(csvFile, fieldIndexes);

        // Materialise filtered row indexes once — all aggregate queries reuse this array.
        // null means no filter (all rows); empty array means the filter matched nothing.
        int[]? rowFilter = filters is { Count: > 0 }
            ? await _repo.GetFilteredRowIndexes(csvFile, filters)
            : null;

        // Short-circuit when the filter produces an empty result set.
        if (rowFilter is { Length: 0 }) return [];

        var identifier  = fields.Where(f => f.Categories.Contains(Category.Identifier)).ToList();
        var categorical = fields.Where(f => f.Categories.Contains(Category.Categorical)).ToList();
        var numericAll  = fields.Where(f => f.Categories.Contains(Category.Numeric) && !f.Categories.Contains(Category.Identifier)).ToList();

        // ── Scale-group numeric fields so fields with compatible IQR/median share a panel ──
        // Each group is a list of fields that can be plotted on the same y-axis.
        var scaleInfos = new (double Median, double IQR)[numericAll.Count];
        for (int i = 0; i < numericAll.Count; i++)
            scaleInfos[i] = await _repo.GetScaleInfo(numericAll[i], rowFilter);
        var numericGroups = new List<List<Field>>();
        var groupScales   = new List<(double Median, double IQR)>();
        for (int i = 0; i < numericAll.Count; i++)
        {
            var fi = scaleInfos[i];
            bool placed = false;
            for (int g = 0; g < numericGroups.Count; g++)
            {
                var cg = groupScales[g];
                double maxIQR = Math.Max(cg.IQR, fi.IQR);
                double minIQR = Math.Min(cg.IQR, fi.IQR);
                bool sameScale = maxIQR < 1e-10
                    ? Math.Abs(cg.Median - fi.Median) < 1e-10
                    : Math.Abs(cg.Median - fi.Median) / maxIQR <= 1 && maxIQR / minIQR <= 2;
                if (sameScale)
                {
                    numericGroups[g].Add(numericAll[i]);
                    // Recompute group scale as the median of individual medians / average IQR
                    groupScales[g] = ((cg.Median + fi.Median) / 2, (cg.IQR + fi.IQR) / 2);
                    placed = true;
                    break;
                }
            }
            if (!placed) { numericGroups.Add([numericAll[i]]); groupScales.Add(fi); }
        }
        var numeric = numericAll; // flat list for pair-wise detectors

        // Prefetch cardinalities for all categorical fields
        var cards = new int[categorical.Count];
        for (int i = 0; i < categorical.Count; i++)
            cards[i] = await _repo.CountDistinct(categorical[i], rowFilter);
        var cardMap = categorical.Zip(cards).ToDictionary(p => p.First.ID, p => p.Second);

        // Raw data loaded lazily once (for scatter / bubble / correlation)
        Table? rawTable = null;
        async Task<Table> GetRaw()
        {
            if (rawTable != null) return rawTable;
            rawTable = filters is { Count: > 0 }
                ? await _repo.GetWithFilterLimited(csvFile, filters, ScatterSampleSize)
                : await _repo.GetLimited(csvFile, fieldIndexes, ScatterSampleSize);
            return rawTable;
        }

        var res = new List<Chart?>();
        var chartIndex = new Dictionary<string, int>();
        int nextIndex  = 0;
        string Key(params Field[] fs) => string.Join(",", fs.Select(f => f.ID));
        void Reg(string k) { if (!chartIndex.ContainsKey(k)) chartIndex[k] = nextIndex++; }
        void Add(Chart? ch, string k) { if (ch != null) { ch.PanelIndex = chartIndex.GetValueOrDefault(k, -1); res.Add(ch); } }

        // ── 3-field: cat × cat × numeric ─────────────────────────────────────
        for (int ci = 0; ci < categorical.Count; ci++)
        for (int cj = ci + 1; cj < categorical.Count; cj++)
        foreach (var nf in numeric)
        {
            if (nf == categorical[ci] || nf == categorical[cj]) continue;
            bool noDateTime = categorical[ci].Type != typeof(DateTime) && categorical[cj].Type != typeof(DateTime);
            if (noDateTime)
            {
                var k = Key(categorical[ci], categorical[cj], nf); Reg(k);
                if (cardMap[categorical[ci].ID] <= 10 && cardMap[categorical[cj].ID] <= 10)
                    Add(await DetectMatrixAsync(categorical[ci], categorical[cj], rowFilter), k);
                Add(await DetectGroupedBarAsync(nf, categorical[ci], categorical[cj], rowFilter), k);
            }
            else
            {
                var dtF  = categorical[ci].Type == typeof(DateTime) ? categorical[ci] : categorical[cj];
                var catF = categorical[ci].Type == typeof(DateTime) ? categorical[cj] : categorical[ci];
                var k = Key(dtF, catF, nf); Reg(k);
                Add(await DetectTimeSeriesCategoricalNumericAsync(nf, dtF, catF, rowFilter), k);
                Add(await DetectTimeSeriesEnhancedAsync(dtF, new[] { nf }, rowFilter), k);
            }
        }

        // ── 3-field: cat × numeric × identifier ──────────────────────────────
        foreach (var catF in categorical)
        foreach (var nf in numeric)
        {
            foreach (var id in identifier)
            {
                if (id == nf || id == catF) continue;
                var k = Key(catF, nf, id); Reg(k);
                Add(await DetectMixedBarChartAsync(catF, nf, id, rowFilter), k);
            }
        }

        // ── 3-field: cat × numeric × numeric ─────────────────────────────────
        foreach (var catF in categorical)
        for (int ni = 0; ni < numeric.Count; ni++)
        for (int nj = ni + 1; nj < numeric.Count; nj++)
        {
            if (catF == numeric[ni] || catF == numeric[nj]) continue;
            var k = Key(catF, numeric[ni], numeric[nj]); Reg(k);
            Add(await DetectMixedLineChartAsync(catF, numeric[ni], numeric[nj], rowFilter), k);

            // Scatter: each category gets its own colored dataset of {x,y} points
            var raw3 = await GetRaw();
            var fvCat = raw3.Columns.FirstOrDefault(c => c.Field.ID == catF.ID);
            var fvN1  = raw3.Columns.FirstOrDefault(c => c.Field.ID == numeric[ni].ID);
            var fvN2  = raw3.Columns.FirstOrDefault(c => c.Field.ID == numeric[nj].ID);
            if (fvCat != null && fvN1 != null && fvN2 != null)
            {
                var sc = DetectScatter(new List<FieldValues> { fvCat, fvN1, fvN2 });
                if (sc != null) sc.Title = $"{numeric[ni].Name} vs {numeric[nj].Name} by {catF.Name}";
                Add(sc, k);
            }
        }

        // ── 2-field: cat × cat ────────────────────────────────────────────────
        for (int ci = 0; ci < categorical.Count; ci++)
        for (int cj = ci + 1; cj < categorical.Count; cj++)
        {
            var catA = categorical[ci]; var catB = categorical[cj];
            if (catA.Type == typeof(DateTime))
            {
                var k = Key(catA, catB); Reg(k);
                Add(await DetectTimeSeriesCategoricalAsync(catA, catB, rowFilter), k);
            }
            else if (catB.Type == typeof(DateTime))
            {
                var k = Key(catB, catA); Reg(k);
                Add(await DetectTimeSeriesCategoricalAsync(catB, catA, rowFilter), k);
            }
            else
            {
                var k = Key(catA, catB); Reg(k);
                if (cardMap[catA.ID] <= 20 && cardMap[catB.ID] <= 20) { Add(await DetectMatrixAsync(catA, catB, rowFilter), k); Add(await DetectMatrixMeanAsync(catA, catB, rowFilter), k); }
                else Add(await DetectBarChartAsync(catA, catB, rowFilter), k);
            }
        }

        // ── 2-field: cat × numeric ────────────────────────────────────────────
        foreach (var catF in categorical)
        foreach (var nf in numeric)
        {
            if (nf == catF) continue;
            var k = Key(catF, nf); Reg(k);
            if (catF.Type == typeof(DateTime))
            {
                Add(await DetectTimeSeriesLineAsync(catF, new[] { nf }, rowFilter), k);
                Add(await DetectTimeSeriesEnhancedAsync(catF, new[] { nf }, rowFilter), k);
                Add(await DetectCumulativeSumAsync(catF, nf, rowFilter), k);
                Add(await DetectGrowthRateAsync(catF, nf, rowFilter), k);
            }
            else
            {
                Add(cardMap[catF.ID] <= 20 ? await DetectBarChartAsync(catF, nf, rowFilter) : await DetectSideBarAsync(catF, nf, rowFilter), k);
                Add(await DetectRankedBarAsync(catF, nf, rowFilter), k);
                Add(await DetectBoxPlotAsync(catF, nf, rowFilter), k);
            }
        }

        // ── 2-field: cat × multiple same-scale numerics (grouped bar) ────────
        foreach (var catF in categorical)
        foreach (var ng in numericGroups)
        {
            var ss = ng.Where(nf => nf != catF).ToList();
            if (ss.Count <= 1) continue;
            // Emit one grouped bar per extra numeric in the group alongside the first
            for (int ni = 0; ni < ss.Count - 1; ni++)
            {
                var k = Key(catF, ss[ni], ss[ni + 1]); Reg(k);
                Add(await DetectMixedLineChartAsync(catF, ss[ni], ss[ni + 1], rowFilter), k);
            }
        }

        // ── 2-field: numeric × numeric (scatter — needs raw rows) ─────────────
        for (int ni = 0; ni < numeric.Count; ni++)
        for (int nj = ni + 1; nj < numeric.Count; nj++)
        {
            var k = Key(numeric[ni], numeric[nj]); Reg(k);
            var raw  = await GetRaw();
            var fvA  = raw.Columns.FirstOrDefault(c => c.Field.ID == numeric[ni].ID);
            var fvB  = raw.Columns.FirstOrDefault(c => c.Field.ID == numeric[nj].ID);
            if (fvA != null && fvB != null) { var ct = new List<FieldValues> { fvA, fvB }; Add(DetectScatter(ct), k); Add(DetectScatterRegression(ct), k); }
        }

        // ── identifier × numeric ──────────────────────────────────────────────
        foreach (var id in identifier)
        foreach (var nf in numeric)
        {
            if (id == nf) continue;
            var k = Key(id, nf); Reg(k);
            Add(await DetectSideBarAsync(id, nf, rowFilter), k);
        }

        // ── single categorical ────────────────────────────────────────────────
        foreach (var cat in categorical)
        {
            var k = Key(cat); Reg(k);
            if (cat.Type == typeof(DateTime))
                Add(await DetectDateTimeFrequencyAsync(cat, rowFilter), k);
            else
            {
                int dc = cardMap[cat.ID];
                Add(dc <= 20 ? await DetectPieChartAsync(cat, rowFilter) : await DetectSideBarAsync(cat, rowFilter), k);
                if (dc is >= 3 and <= 60) Add(await DetectParetoChartAsync(cat, rowFilter), k);
            }
        }

        // ── single identifier ─────────────────────────────────────────────────
        foreach (var id in identifier) { var k = Key(id); Reg(k); Add(await DetectSideBarAsync(id, rowFilter), k); }

        // ── single numeric ────────────────────────────────────────────────────
        foreach (var nf in numeric) { var k = Key(nf); Reg(k); Add(await DetectHistogramAsync(nf, rowFilter), k); }

        // ── Correlation matrix (3+ numerics — needs raw rows) ─────────────────
        if (numeric.Count >= 3)
        {
            var k = Key(numeric.ToArray()); Reg(k);
            Table corrRaw = filters is { Count: > 0 }
                ? await _repo.GetWithFilterLimited(csvFile, filters, CorrelationSampleSize)
                : await _repo.GetLimited(csvFile, fieldIndexes, CorrelationSampleSize);
            var cols = corrRaw.Columns.Where(c => numeric.Any(nf => nf.ID == c.Field.ID)).ToList();
            if (cols.Count >= 3) Add(DetectCorrelationMatrix(cols), k);
        }

        // ── Bubble chart (every triple of numerics — needs raw rows) ──────────
        if (numeric.Count >= 3)
        {
            for (int bi = 0; bi < numeric.Count; bi++)
            for (int bj = bi + 1; bj < numeric.Count; bj++)
            for (int bk = bj + 1; bk < numeric.Count; bk++)
            {
                var k     = Key(numeric[bi], numeric[bj], numeric[bk]);
                var scKey = Key(numeric[bi], numeric[bj]);
                if (!chartIndex.ContainsKey(k)) chartIndex[k] = chartIndex.GetValueOrDefault(scKey, nextIndex++);
                var raw = await GetRaw();
                var fvA = raw.Columns.FirstOrDefault(c => c.Field.ID == numeric[bi].ID);
                var fvB = raw.Columns.FirstOrDefault(c => c.Field.ID == numeric[bj].ID);
                var fvC = raw.Columns.FirstOrDefault(c => c.Field.ID == numeric[bk].ID);
                if (fvA != null && fvB != null && fvC != null)
                    Add(DetectBubbleChart(new List<FieldValues> { fvA, fvB, fvC }), k);
            }
        }

        return res.Where(c => c != null).Select(c => c!).ToList();
    }
}