using System.Diagnostics;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.ChartModels;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace  LQ.Tests;

[TestClass]
public class AutoChartTest
{
    private IAnalystService _analystService;
    private ICSVService _csvService;
    private IAutoChartService _autoChartService;
    public TestContext TestContext { get; set; }
    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        var services = new ServiceCollection();
        services.AddDbContext<LQDbContext>(options => options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<ILQDbService, LQDbService>();
        services.AddScoped<IAnalystService, AnalystService>();
        services.AddScoped<ICSVService, CSVService>();
        services.AddScoped<IAutoChartService, AutoChartService>();
        var provider = services.BuildServiceProvider();
        _analystService = provider.GetService<IAnalystService>();
        _csvService = provider.GetService<ICSVService>();
        _autoChartService = provider.GetService<IAutoChartService>();
    }
    
    [TestMethod]
    public void DetectChartTest()
    {
        Analyst lam = _analystService.GetAnalyst(1);
        Table tempTable = _csvService.GetTable(lam.ID, "hello2.csv");
        List<int> fieldindexes = new List<int> { 3 };
        foreach (FieldValues fvs in tempTable.Columns)
        {
            _autoChartService.DetectCategory(fvs);
        }
        List<Chart> res = _autoChartService.DetectChart(tempTable.CSVFile.ID, fieldindexes);
        
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
        {
            dir = dir.Parent;
        }
        var logPath = Path.Combine(dir!.FullName, "test.log");
        // File.WriteAllText(logPath, string.Empty);
        File.AppendAllText(logPath, "--------------------------------------------\n");
        foreach (Chart c in res)
        {
            File.AppendAllText(logPath, c.ShowJson() + "\n");
        }
        File.AppendAllText(logPath, "end--------------------------------------------\n");
    }
}