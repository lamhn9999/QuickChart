using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

// AddControllers
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
// AddDbContext
builder.Services.AddDbContext<LQDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<Analyst>()
    .AddEntityFrameworkStores<LQDbContext>()
    .AddSignInManager();
// Authen, Author
builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
// DI
builder.Services.AddScoped<ILQDbService, LQDbService>();
builder.Services.AddScoped<ILQIdentityDbService, LQIdentityDbService>();
builder.Services.AddScoped<IAnalystService, AnalystService>();
builder.Services.AddScoped<ICSVService, CSVService>();
builder.Services.AddScoped<IAutoChartService, AutoChartService>();
// AddSession
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.MapControllers();
app.UseStaticFiles();
app.UseSession();

app.UseSwagger();
app.UseSwaggerUI();

app.Run("http://0.0.0.0:5000");

// using (var scope = app.Services.CreateScope())
// {
//     var services = scope.ServiceProvider;
//     var analystService = services.GetRequiredService<IAnalystService>();
//     var csvService = services.GetRequiredService<ICSVService>();
//     var lqdbService = services.GetRequiredService<ILQDbService>();
// }