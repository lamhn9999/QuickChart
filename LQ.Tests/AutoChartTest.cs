using System.Text.Json;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Models.ChartModels;
using LQ.Models.DTOs;
using LQ.Models.TableModels;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
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
    private string logPath { get; set; }
    
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
        
        services.AddScoped<ILQDbService, LQDbService>();
        services.AddScoped<ILQIdentityDbService, LQIdentityDbService>();
        services.AddScoped<IAnalystService, AnalystService>();
        services.AddScoped<ICSVService, CSVService>();
        services.AddScoped<IAutoChartService, AutoChartService>();
        
        
        var provider = services.BuildServiceProvider();
        _analystService = provider.GetRequiredService<IAnalystService>();
        _csvService = provider.GetRequiredService<ICSVService>();
        _autoChartService = provider.GetRequiredService<IAutoChartService>();
        
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.GetFiles("*.csproj").Any())
        {
            dir = dir.Parent;
        }
        logPath = Path.Combine(dir!.FullName, "test.log");
    }
    
    // [TestMethod]
    // public void DetectChartTest()
    // {
    //     File.WriteAllText(logPath, String.Empty);
    //     Analyst tester = _analystService.GetAnalyst(BaseDTO.DefaultAnalystName);
    //     Table tempTable = _csvService.GetTable(tester.ID, "top_rated_2000webseries.csv");
    //     List<int> fieldindexes = new List<int> { 9 };
    //     List<Chart> res = _autoChartService.DetectChart(tempTable.CSVFile.ID, fieldindexes);
    //     
    //     File.AppendAllText(logPath, "--------------------------------------------\n");
    //     foreach (FieldValues fv in tempTable.Columns)
    //     {
    //         if(fieldindexes.Contains(fv.Field.Index))
    //             File.AppendAllText(logPath, JsonSerializer.Serialize(fv.Field.Categories) + "\n");
    //     }
    //     foreach (Chart c in res)
    //     {
    //         File.AppendAllText(logPath, c.ShowJson() + "\n");
    //     }
    //     File.AppendAllText(logPath, "end--------------------------------------------\n");
    // }

    [TestMethod]
    public async Task NumericTest()
    {
        File.WriteAllText(logPath, String.Empty);
        Analyst tester = await _analystService.FindAnalystByName("BuiThanhLam");
        Table tempTable = _csvService.GetTable(tester.Id, "student_data.csv");
        List<int> fieldindexes = new List<int> { 12, 13 };
        List<Chart> res = _autoChartService.DetectChart(tempTable.CSVFile.ID, fieldindexes);
        
        File.AppendAllText(logPath, "--------------------------------------------\n");
        foreach (FieldValues fv in tempTable.Columns)
        {
            if(fieldindexes.Contains(fv.Field.Index))
                File.AppendAllText(logPath, JsonSerializer.Serialize(fv.Field.Categories) + "\n");
        }
        foreach (Chart c in res)
        {
            File.AppendAllText(logPath, c.ShowJson() + "\n");
        }
        File.AppendAllText(logPath, "end--------------------------------------------\n");   
    }
}