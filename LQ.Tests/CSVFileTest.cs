using System.Diagnostics;
using LQ.Models.AnalystModels;
using LQ.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using LQ.Services.Interfaces;
using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;
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

        services.AddAuthentication();
        services.AddIdentityCore<Analyst>()
            .AddEntityFrameworkStores<LQDbContext>()
            .AddSignInManager();

        services.AddScoped<IAnalystService, AnalystService>();
        services.AddScoped<ICSVService, CSVService>();
        services.AddScoped<TableRepositoryAbstract, TableRepository>();
        services.AddScoped<IAutoChartService, AutoChartService>();

        var provider = services.BuildServiceProvider();
        _analystService = provider.GetRequiredService<IAnalystService>();
        _csvService = provider.GetRequiredService<ICSVService>();
        _autoChartService = provider.GetRequiredService<IAutoChartService>();
    }

    [TestMethod]
    public void InjectDependency_ReturnNotNull()
    {
        Assert.IsNotNull(_csvService);
    }

    // [TestMethod]
    // public async Task DetectDateTime()
    // {
    //     Analyst Hung = await _analystService.GetAnalystByName("N D Hung");
    //     Table earthquake = _csvService.GetTable(Hung.Id, "Earthquake.csv");
    //     _csvService.DetectDateTime(earthquake.Columns[0]);
    //
    // }

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

    // [TestMethod]
    // public async Task RemoveTest()
    // {
    //     Analyst tester = await _analystService.FindAnalystById(BaseDTO.DefaultAnalystID);
    //     // bool temp = await _csvService.ProcessUploadedCSV(tester,
    //     //     "/home/lam/LQ/LQ/Data/SampleCSVs/top_rated_2000webseries.csv");
    //     // Assert.IsTrue(temp);
    //     Debug.WriteLine(tester);
    //     var list = _lqDbService.GetCSVFiles(tester);
    //     foreach(var l in list)
    //     {
    //         Debug.WriteLine(l.Name);
    //     }
    //     Debug.WriteLine(_lqDbService.GetCSVFile(tester, "top_rated_2000webseries.csv"));
    //     int newid = _lqDbService.GetCSVFile(tester, "top_rated_2000webseries.csv").ID;
    //     _lqDbService.RemoveCSVFile(newid);
    //     // Assert(_lqDbService.GetCSVFile(tester, "top_rated_2000webseries.csv") == null);
    // }
    //
    // [TestMethod]
    // public async Task RenameTest()
    // {
    //     Analyst tester = await _analystService.FindAnalystById(BaseDTO.DefaultAnalystID);
    //     bool temp = await _csvService.ProcessUploadedCSV(tester,
    //         "/home/lam/LQ/LQ/Data/SampleCSVs/top_rated_2000webseries.csv");
    //     Assert.IsTrue(temp);
    //     int newid = _lqDbService.GetCSVFile(tester, "top_rated_2000webseries.csv").ID;
    //     _lqDbService.RenameCSVFile(newid, "top_rated_movies.csv");
    //     Assert.IsNotNull(_lqDbService.GetCSVFile(tester, "top_rated_movies.csv"));
    // }
    // [TestMethod]
    // public async Task UploadTest()
    // {
    //     Analyst tester = await _analystService.FindAnalystById(BaseDTO.DefaultAnalystID);
    //     bool temp = await _csvService.ProcessUploadedCSV(tester,
    //         "/home/lam/LQ/LQ/Data/SampleCSVs/player_stats_2023.csv");
    // }
    // FilterTest moved to TableRepositoryTest.cs (uses TableRepositoryAbstract.GetWithFilter)
}