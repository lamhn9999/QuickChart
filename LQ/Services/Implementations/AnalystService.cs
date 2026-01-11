using System.Diagnostics;
using LQ.Data.Database;
using LQ.Services.Interfaces;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Implementations;

public class AnalystService : LQIdentityDbService, IAnalystService
{
    private readonly ILQDbService _lqDbService;

    public AnalystService(ILQDbService lqDbService, 
        LQDbContext lqDbContext, 
        UserManager<Analyst> analystManager, 
        SignInManager<Analyst> analystSignInManager)
        : base(lqDbContext, analystManager, analystSignInManager)
    {
        _lqDbService = lqDbService;
    }

    public List<CSVFile> GetCSVFiles(string analystid)
    {
        return _lqDbService.GetCSVFiles(analystid);
    }
}