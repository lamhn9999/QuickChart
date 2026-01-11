using LQ.Data.Database;
using LQ.Models.AnalystModels;
using LQ.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Implementations;

public class LQIdentityDbService : ILQIdentityDbService
{
    private readonly LQDbContext _lqDbContext;
    private readonly UserManager<Analyst> _analystManager;
    private readonly SignInManager<Analyst> _analystSignInManager;

    public LQIdentityDbService(LQDbContext lqDbContext, UserManager<Analyst> analystManager, SignInManager<Analyst> analystSignInManager)
    {
        _lqDbContext = lqDbContext;
        _analystManager = analystManager;
        _analystSignInManager = analystSignInManager;
    }
    
    public async Task<IdentityResult> AddAnalyst(Analyst analyst)
    {
        return await _analystManager.CreateAsync(analyst);
    }

    public async Task<IdentityResult> AddAnalyst(Analyst analyst, string password)
    {
        return await _analystManager.CreateAsync(analyst, password);
    }
    public async Task<Analyst?> FindAnalystByName(string name)
    {
        return await _analystManager.FindByNameAsync(name);
    }
    public async Task<Analyst?> FindAnalystById(string id)
    {
        return await _analystManager.FindByIdAsync(id);
    }
    public async Task<Analyst?> FindAnalystByEmail(string email)
    {
        return await _analystManager.FindByEmailAsync(email);
    }

    public async Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password)
    {
        return await _analystSignInManager.CheckPasswordSignInAsync(analyst, password, lockoutOnFailure: true);
    }
}