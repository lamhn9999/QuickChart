using LQ.Data.Database;
using LQ.Models.TableModels;
using LQ.Models.AnalystModels;
using LQ.Models.DTOs;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// AddControllers
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
// AddDbContext
builder.Services.AddDbContext<LQDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// DI
builder.Services.AddScoped<ILQDbService, LQDbService>();
builder.Services.AddScoped<ICSVService, CSVService>();
builder.Services.AddScoped<IAnalystService, AnalystService>();
// AddSession
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();

var app = builder.Build();
app.MapControllers();
app.UseStaticFiles();
app.UseSession();
// app.Run("http://0.0.0.0:5000");

// using (var scope = app.Services.CreateScope())
// {
//     var services = scope.ServiceProvider;
//     var analystService = services.GetRequiredService<IAnalystService>();
//     var csvService = services.GetRequiredService<ICSVService>();
//     var lqdbService = services.GetRequiredService<ILQDbService>();
// }