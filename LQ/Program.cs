using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Models.AnalystModels;
using LQ.Services;
using LQ.Services.Implementations;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Http.Features;
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

// AddIdentityCore
builder.Services.AddIdentityCore<Analyst>(options =>
    {
        // Password constraints
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true; // Requires at least one symbol (!, @, #)
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        // Email / User uniqueness constraints
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";
        options.User.RequireUniqueEmail = true; 
        
        // Lockout settings (helps prevent Brute Force attacks)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<LQDbContext>()
    .AddSignInManager();

builder.Services.Configure<FormOptions>(options =>
{
    // Set limit to 200MB
    options.MultipartBodyLengthLimit = 209715200;
});

// Authen, Author
builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
// DI
builder.Services.AddScoped<IAnalystService, AnalystService>();
builder.Services.AddScoped<ICSVService, CSVService>();
builder.Services.AddScoped<TableRepositoryAbstract, TableRepository>();
builder.Services.AddScoped<IAutoChartService, AutoChartService>();
builder.Services.AddSingleton<SparklineCancelService>();
builder.Services.AddHostedService<GuestCleanupService>();
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