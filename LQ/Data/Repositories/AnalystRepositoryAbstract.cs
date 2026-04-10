using LQ.Models.AnalystModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Data.Repositories;

public abstract class AnalystRepositoryAbstract
{
    protected abstract Task<IdentityResult> Add(Analyst analyst, string password);
    public abstract Task<Analyst?> FindByName(string name);
    public abstract Task<Analyst?> FindById(string id);
    public abstract Task<Analyst?> FindByEmail(string email);
    public abstract Task<SignInResult> CheckPasswordSignIn(Analyst analyst, string password);
}