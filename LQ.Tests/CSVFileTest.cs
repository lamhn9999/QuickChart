using System.Diagnostics;
using LQ.Models.AnalystModels;
using LQ.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using LQ.Services.Interfaces;
using LQ.Data.Database;
using LQ.Models.TableModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LQ.Tests;

[TestClass]
public sealed class CSVFileTest
{
    private ICSVService? _csvService;
    private IAnalystService? _analystService;
    private IAutoChartService? _autoChartService;
    
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
        // Analyst _tester = _analystService.GetAnalyst("Tester");
    }
    
    [TestMethod]
    public void InjectDependency_ReturnNotNull()
    {
        Assert.IsNotNull(_csvService);
    }

    // [TestMethod]
    // public void ProcessUploadedCSV_ReturnFalse()
    // {
    //     Analyst tester = _analystService.GetAnalyst("Tester");
    //     Assert.IsFalse(_csvService.ProcessUploadedCSV(tester, "/home/lam/LQ/LQ/Data/SampleCSVs/hello1.csv"));
    // }

    [TestMethod]
    public void ProcessUploadedCSV_ReturnTrue()
    {
        Analyst tester = _analystService.GetAnalyst("Tester");
        Assert.IsTrue(_csvService.ProcessUploadedCSV(tester, "/home/lam/LQ/LQ/Data/SampleCSVs/hello3.csv"));
    }

    [TestMethod]
    public void Validate_ReturnTrue()
    {
        Analyst tester = _analystService.GetAnalyst("N D Hung");
        Assert.IsTrue(_csvService.Validate(tester, "/home/lam/LQ/LQ/Data/SampleCSVs/spotify_data_cleaned.csv"));
    }
}