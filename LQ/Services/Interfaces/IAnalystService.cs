using LQ.Models.AnalystModels;
using LQ.Models.TableModels;
using Microsoft.AspNetCore.Identity;

namespace LQ.Services.Interfaces;

public interface IAnalystService : ILQIdentityDbService
{
    public List<CSVFile> GetCSVFiles(string analystid);
}