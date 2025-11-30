using LQ.Models.DTOs;

namespace LQ.Services.Interfaces;

public interface ITableService
{
    TableModel InterpretCsv(string path);
}