using System.Diagnostics;
using System.Text.Json;
using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LQ.Tests;

/// <summary>
/// Integration tests for TableRepository and AutoChartService.
/// Uses real Postgres data — no sign-in or analyst lookup needed.
/// Change the CSV_FILE_ID constant to any CSV file ID that exists in your DB.
/// </summary>
[TestClass]
public class TableRepositoryTest
{
    private const int CSV_FILE_ID = 34;

    private static readonly List<int> AllFields    = [0, 1, 2, 3];
    private static readonly List<int> CatFields    = [0, 1];
    private static readonly List<int> NumericFields = [2, 3];

    private TableRepositoryAbstract _repo = null!;
    private IAutoChartService _autoChart  = null!;
    private string _logPath = "";

    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddDbContext<LQDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<TableRepositoryAbstract, TableRepository>();
        services.AddScoped<IAutoChartService, AutoChartService>();

        var provider = services.BuildServiceProvider();
        _repo      = provider.GetRequiredService<TableRepositoryAbstract>();
        _autoChart = provider.GetRequiredService<IAutoChartService>();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
            dir = dir.Parent;
        _logPath = Path.Combine(dir!.FullName, "table_repo_test.log");
    }


    [TestMethod]
    public async Task GetFile_ReturnsFile()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        Assert.IsNotNull(file);
        Debug.WriteLine($"File: id={file.ID}  name={file.Name}");
    }

    [TestMethod]
    public async Task GetDisplayTable()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        var dto = await _repo.GetDisplayLimited(file, 200);
        Debug.WriteLine($"File: {dto.FileName} | Rows: {dto.RowCount}");
        Debug.WriteLine(string.Join(" | ", dto.Fields));
        foreach (var row in dto.Values)
            Debug.WriteLine(string.Join(" | ", row));
        Assert.AreEqual(200, dto.RowCount);
    }


    [TestMethod]
    public async Task GetFields_ReturnsFields()
    {
        var file   = await _repo.GetFile(CSV_FILE_ID);
        var fields = await _repo.GetFields(file, AllFields);
        Assert.IsTrue(fields.Count > 0);
        foreach (var f in fields)
            Debug.WriteLine($"  Field[{f.Index}] {f.Name}  type={f.TypeName}  cats={string.Join(",", f.Categories)}");
    }

    [TestMethod]
    public async Task CountByGroup_CategoricalField_ReturnsRows()
    {
        var file   = await _repo.GetFile(CSV_FILE_ID);
        var fields = await _repo.GetFields(file, CatFields);
        if (fields.Count == 0) Assert.Inconclusive("No fields found for given indexes.");

        var field  = fields[0];
        var rows   = await _repo.CountByGroup(field);

        Debug.WriteLine($"CountByGroup({field.Name}): {rows.Count} groups");
        foreach (var r in rows.Take(10))
            Debug.WriteLine($"  {r.Key} → {r.Count}");

        Assert.IsTrue(rows.Count > 0);
    }


    [TestMethod]
    public async Task AverageByGroup_CatAndNumeric_ReturnsRows()
    {
        var file    = await _repo.GetFile(CSV_FILE_ID);
        var catF    = (await _repo.GetFields(file, CatFields)).FirstOrDefault();
        var numF    = (await _repo.GetFields(file, NumericFields)).FirstOrDefault();

        if (catF == null || numF == null)
            Assert.Inconclusive("Need at least one categorical and one numeric field.");

        var rows = await _repo.AverageByGroup(numF, catF);
        Debug.WriteLine($"AverageByGroup({numF.Name} BY {catF.Name}): {rows.Count} groups");
        foreach (var r in rows.Take(10))
            Debug.WriteLine($"  {r.Key} → avg={r.Average:F4}");

        Assert.IsTrue(rows.Count > 0);
    }


    [TestMethod]
    public async Task FiveNumberSummary_ReturnsData()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        var catF = (await _repo.GetFields(file, CatFields)).FirstOrDefault();
        var numF = (await _repo.GetFields(file, NumericFields)).FirstOrDefault();

        if (catF == null || numF == null)
            Assert.Inconclusive("Need at least one categorical and one numeric field.");

        var rows = await _repo.FiveNumberSummaryByGroup(numF, catF);
        Debug.WriteLine($"BoxPlot({numF.Name} BY {catF.Name}): {rows.Count} groups");
        foreach (var r in rows.Take(5))
            Debug.WriteLine($"  {r.Key}: min={r.Min:F2} Q1={r.Q1:F2} median={r.Median:F2} Q3={r.Q3:F2} max={r.Max:F2}");

        Assert.IsTrue(rows.Count > 0);
    }


    [TestMethod]
    public async Task MinMax_NumericField_ReturnsRange()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        var numF = (await _repo.GetFields(file, NumericFields)).FirstOrDefault();

        if (numF == null) Assert.Inconclusive("No numeric field found.");

        var (min, max) = await _repo.MinMax(numF);
        Debug.WriteLine($"MinMax({numF.Name}): min={min}  max={max}");
        Assert.IsTrue(max >= min);
    }


    [TestMethod]
    public async Task GetScaleInfo_NumericField_ReturnsMedianAndIQR()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        var numF = (await _repo.GetFields(file, NumericFields)).FirstOrDefault();

        if (numF == null) Assert.Inconclusive("No numeric field found.");

        var (median, iqr) = await _repo.GetScaleInfo(numF);
        Debug.WriteLine($"ScaleInfo({numF.Name}): median={median:F4}  IQR={iqr:F4}");
        Assert.IsTrue(median is not double.NaN);
    }


    [TestMethod]
    public async Task CountDistinct_CategoricalField_ReturnsCardinality()
    {
        var file  = await _repo.GetFile(CSV_FILE_ID);
        var catF  = (await _repo.GetFields(file, CatFields)).FirstOrDefault();

        if (catF == null) Assert.Inconclusive("No categorical field found.");

        int card = await _repo.CountDistinct(catF);
        Debug.WriteLine($"CountDistinct({catF.Name}): {card}");
        Assert.IsTrue(card > 0);
    }


    [TestMethod]
    public async Task GetHistogramBuckets_NumericField_ReturnsBuckets()
    {
        var file = await _repo.GetFile(CSV_FILE_ID);
        var numF = (await _repo.GetFields(file, NumericFields)).FirstOrDefault();

        if (numF == null) Assert.Inconclusive("No numeric field found.");

        var (min, max) = await _repo.MinMax(numF);
        var buckets    = await _repo.GetHistogramBuckets(numF, min, max, 20);

        Debug.WriteLine($"Histogram({numF.Name}): {buckets.Count} non-empty buckets out of 20");
        foreach (var (bucket, count) in buckets)
            Debug.WriteLine($"  bucket[{bucket}] = {count}");

        Assert.IsTrue(buckets.Count > 0);
    }

    [TestMethod]
    public async Task DetectChartAsync_AllFields_ReturnsCharts()
    {
        File.WriteAllText(_logPath, string.Empty);

        var charts = await _autoChart.DetectChartAsync(CSV_FILE_ID, AllFields);

        File.AppendAllText(_logPath, $"=== DetectChartAsync(csvFileId={CSV_FILE_ID}, fields={string.Join(",", AllFields)}) ===\n");
        File.AppendAllText(_logPath, $"Total charts: {charts.Count}\n\n");
        foreach (var c in charts)
        {
            File.AppendAllText(_logPath, c.ShowJson() + "\n");
            Debug.WriteLine($"  {c.ChartType,-12}  panel={c.PanelIndex}  title={c.Title}");
        }

        Assert.IsTrue(charts.Count > 0, "Expected at least one chart.");
    }


    [TestMethod]
    public async Task DetectChartAsync_CatOnly_ReturnsPieOrSideBar()
    {
        var charts = await _autoChart.DetectChartAsync(CSV_FILE_ID, CatFields);

        Debug.WriteLine($"Cat-only charts: {charts.Count}");
        foreach (var c in charts)
            Debug.WriteLine($"  {c.ChartType}  panel={c.PanelIndex}");

        Assert.IsTrue(
            charts.Any(c => c.ChartType is ChartType.Pie or ChartType.SideBar or ChartType.MixedBar),
            "Expected Pie, SideBar, or Pareto chart for categorical fields.");
    }

    [TestMethod]
    public async Task DetectChartAsync_NumericOnly_ReturnsHistogramOrScatter()
    {
        var charts = await _autoChart.DetectChartAsync(CSV_FILE_ID, NumericFields);

        Debug.WriteLine($"Numeric-only charts: {charts.Count}");
        foreach (var c in charts)
            Debug.WriteLine($"  {c.ChartType}  panel={c.PanelIndex}  title={c.Title}");

        Assert.IsTrue(
            charts.Any(c => c.ChartType is ChartType.Histogram or ChartType.Scatter),
            "Expected Histogram or Scatter chart for numeric fields.");
    }


    [TestMethod]
    public async Task DetectChartAsync_WithFilter_ReturnsCharts()
    {
        // Adjust FilteringValues to match real values in your dataset
        var filters = new List<ValueFilterDTO>
        {
            new() { FieldIndex = 1, FilteringValues = ["2019", "2020"] },
        };

        var charts = await _autoChart.DetectChartWithFiltersAsync(CSV_FILE_ID, AllFields, filters);

        Debug.WriteLine($"Filtered charts: {charts.Count}");
        foreach (var c in charts)
            Debug.WriteLine($"  {c.ChartType}  panel={c.PanelIndex}");

        // Filter may legitimately produce 0 charts if the values don't exist;
        // we just assert it doesn't throw.
        Assert.IsNotNull(charts);
    }
}
