using LQ.Models.AnalystModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Interfaces;

public interface ILQIdentityDbService
{
    public Task<IdentityResult> AddAnalyst(Analyst analyst);
    public Task<IdentityResult> AddAnalyst(Analyst analyst, string password);
    public Task<Analyst?> FindAnalystByName(string name);
    public Task<Analyst?> FindAnalystById(string id);
    public Task<Analyst?> FindAnalystByEmail(string email);
    public Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password);
}