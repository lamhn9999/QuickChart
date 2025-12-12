using LQ.Data.Database;
using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<LQDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ILQDbService, LQDbService>();
builder.Services.AddScoped<ICSVService, CSVService>();
builder.Services.AddScoped<IAnalystService, AnalystService>();

var app = builder.Build();
app.MapControllers();
app.UseStaticFiles();
// app.Run();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var analystService = services.GetRequiredService<IAnalystService>();
    var csvService = services.GetRequiredService<ICSVService>();
    Analyst Lam = analystService.GetAnalyst(1);
    csvService.ProcessUploadedCSV(Lam, BaseDTO.SampleCSVsPath + "/hello2.csv");
}