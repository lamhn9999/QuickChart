using LQ.Data.Database;
using LQ.Data.Repositories;
using LQ.Services.Interfaces;
using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Implementations;

public class AnalystService : AnalystRepository, IAnalystService
{
    public AnalystService(
        LQDbContext lqDbContext, 
        UserManager<Analyst> analystManager, 
        SignInManager<Analyst> analystSignInManager)
        : base(lqDbContext, analystManager, analystSignInManager)
    {
    }

    public new async Task<IdentityResult> Add(Analyst analyst, string password)
    {
        return await base.Add(analyst, password);
    }

    public new async Task<Analyst?> FindByName(string name)
    {
        return await base.FindByName(name);
    }

    public new async Task<Analyst?> FindById(string id)
    {
        return await base.FindById(id);
    }

    public new async Task<Analyst?> FindByEmail(string email)
    {
        return await base.FindByEmail(email);
    }

    public new async Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password)
    {
        return await base.CheckPasswordSignIn(analyst, password);
    }
}