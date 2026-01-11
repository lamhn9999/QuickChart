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

        services.AddScoped<ILQDbService, LQDbService>();
        services.AddScoped<ILQIdentityDbService, LQIdentityDbService>();
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
    [DataRow("Lam.H.N")]
    public async Task InsertAnalyst_ReturntTrue(string value)
    {
        Analyst analyst = Analyst.GiveBirth(value);
        analyst.Email = "hongoclamm@gmail.com";
        IdentityResult res = await _analystService.AddAnalyst(analyst, "Lam@123");
        Debug.WriteLine(res.ToString());
        Assert.IsTrue(res.Succeeded);
    }

    [TestMethod]
    [DataRow("Tung.T.H")]
    public async Task FindAnalyst_ReturnNotNull(string value)
    {
        Analyst? analyst = await _analystService.FindAnalystByName(value);
        Assert.IsNotNull(analyst);
        if(analyst is not null) 
            Debug.WriteLine(analyst.Id);
    }
}
