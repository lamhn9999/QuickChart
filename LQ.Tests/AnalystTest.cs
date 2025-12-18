using LQ.Models.AnalystModels;
using LQ.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using LQ.Services.Interfaces;
using LQ.Data.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LQ.Tests;

[TestClass]
public sealed class AnalystTest
{
    private IAnalystService? _analystService;
    
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
        var provider = services.BuildServiceProvider();
        _analystService = provider.GetService<IAnalystService>();
    }
    
    [TestMethod]
    [DataRow("*Ta Hai Tung*")]
    [DataRow("TaHai.Tung")]
    [DataRow("T&T")]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("‎")]
    [DataRow("\n\n\n\r\t")]
    public void GiveBirth_ReturnNull(string value)
    {
        Assert.IsNull(Analyst.GiveBirth(value));
    }

    [TestMethod]
    [DataRow("Ta Hai Tung")]
    public void GiveBirth_ReturnNotNull(string value)
    {
        Assert.IsNotNull(Analyst.GiveBirth(value));
    }

    [TestMethod]
    public void InjectDependency_ReturnNotNull()
    {
        Assert.IsNotNull(_analystService);
    }
    
    // [TestMethod]
    // [DataRow("Tester")]
    // public void InsertAnalyst_ReturntTrue(string value)
    // {
    //     Analyst analyst = Analyst.GiveBirth(value);
    //     Assert.IsTrue(_analystService.InsertAnalyst(analyst));
    // }
}
