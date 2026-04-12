using System.Diagnostics;
using LQ.Models.AnalystModels;
using LQ.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using LQ.Services.Interfaces;
using LQ.Data.Database;
using Microsoft.AspNetCore.Identity;
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

        services.AddAuthentication();
        services.AddIdentityCore<Analyst>()
            .AddEntityFrameworkStores<LQDbContext>()
            .AddSignInManager();

        services.AddScoped<IAnalystService, AnalystService>();
        
        var provider = services.BuildServiceProvider();
        _analystService = provider.GetRequiredService<IAnalystService>();
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
        Debug.WriteLine(value);
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
    
    [TestMethod]
    [DataRow("Ivan.B")]
    public async Task InsertAnalyst_ReturntTrue(string value)
    {
        Analyst analyst = Analyst.GiveBirth(value);
        IdentityResult res = await _analystService.Add(analyst, "Ab@123");
        Debug.WriteLine(res.ToString());
    }

    [TestMethod]
    [DataRow("Tung.T.H")]
    public async Task FindAnalyst_ReturnNotNull(string value)
    {
        Analyst? analyst = await _analystService.FindByName(value);
        Assert.IsNotNull(analyst);
        if(analyst is not null)
            Debug.WriteLine(analyst.Id);
    }
}
