using LQ.Data.Repositories;
using LQ.Data.Database;
using LQ.Models.AnalystModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Data.Repositories;

public class AnalystRepository : AnalystRepositoryAbstract
{
    private readonly LQDbContext _lqDbContext;
    private readonly UserManager<Analyst> _analystManager;
    private readonly SignInManager<Analyst> _analystSignInManager;

    protected AnalystRepository(LQDbContext lqDbContext, UserManager<Analyst> analystManager, SignInManager<Analyst> analystSignInManager)
    {
        _lqDbContext = lqDbContext;
        _analystManager = analystManager;
        _analystSignInManager = analystSignInManager;
    }

    protected override async Task<IdentityResult> Add(Analyst analyst, string password)
    {
        return await _analystManager.CreateAsync(analyst, password);
    }
    
    public override async Task<Analyst?> FindByName(string name)
    {
        return await _analystManager.FindByNameAsync(name);
    }
    
    public override async Task<Analyst?> FindById(string id)
    {
        return await _analystManager.FindByIdAsync(id);
    }
    
    public override async Task<Analyst?> FindByEmail(string email)
    {
        return await _analystManager.FindByEmailAsync(email);
    }

    public override async Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password)
    {
        return await _analystSignInManager.CheckPasswordSignInAsync(analyst, password, lockoutOnFailure: true);
    }
}