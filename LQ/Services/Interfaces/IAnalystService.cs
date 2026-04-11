using LQ.Models.AnalystModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Interfaces;

public interface IAnalystService
{
    public Task<IdentityResult> Add(Analyst analyst, string password);
    public Task<Analyst?> FindByName(string name);
    public Task<Analyst?> FindById(string id);
    public Task<Analyst?> FindByEmail(string email);
    public Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password);
}