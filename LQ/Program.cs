using LQ.Models.DTOs;
using LQ.Services.Implementations;

// var builder = WebApplication.CreateBuilder(args);
// builder.Services.AddControllers();
// builder.Services.AddControllersWithViews();
// var app = builder.Build();
// app.MapControllers();
// app.Run();

TableModel SpotifyData = TableService.InterpretCsv("/home/lam/LQ/LQ/Data/SampleCSVs/hello.csv");
SpotifyData.Show();
