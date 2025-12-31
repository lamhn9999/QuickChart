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
    private ILQDbService? _lqDbService;
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
        _lqDbService = provider.GetService<ILQDbService>();
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

    [TestMethod]
    public void DetectDateTime()
    {
        Analyst Hung = _analystService.GetAnalyst("N D Hung");
        Table earthquake = _csvService.GetTable(Hung.ID, "Earthquake.csv");
        _csvService.DetectDateTime(earthquake.Columns[0]);

    }

    // [TestMethod]
    // public void ProcessUploadedCSV_ReturnTrue()
    // {
    //     Analyst tester = _analystService.GetAnalyst("Lam");
    //     Assert.IsTrue(_csvService.ProcessUploadedCSV(tester, "/home/lam/LQ/LQ/Data/SampleCSVs/top_rated_2000webseries.csv"));
    // }

    // [TestMethod]
    // public void DetectCategoryTest()
    // {
    //     Analyst tester = _analystService.GetAnalyst("Lam");
    //     Table table = _csvService.GetTable(tester.ID, "spotify_data_cleaned.csv");
    //     FieldValues fieldvalues = (from fv in table.Columns where fv.Field.Name == "artist_genres" select fv).FirstOrDefault();
    //     _csvService.DetectCategory(fieldvalues);
    // }

    // [TestMethod]
    // public void Validate_ReturnTrue()
    // {
    //     Analyst tester = _analystService.GetAnalyst("N D Hung");
    //     Assert.IsTrue(_csvService.Validate(tester, "/home/lam/LQ/LQ/Data/SampleCSVs/spotify_data_cleaned.csv"));
    // }
    //
    // [TestMethod]
    // public void DetectCategoryListTest()
    // {
    //     Analyst lam = _analystService.GetAnalyst("Lam");
    //     Table spotify = _csvService.GetTable(lam.ID, "spotify_data_cleaned.csv");
    //     FieldValues fv = (from f in spotify.Columns where f.Field.Name == "artist_genres" select f).FirstOrDefault();
    //     _csvService.DetectCategoryList(fv);
    // }
}