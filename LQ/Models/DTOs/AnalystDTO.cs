using System.Text.Json;
namespace LQ.Models.DTOs;

public class AnalystDTO
{
    public string ID { get; set; }
    public string Name { get; set; }
    public int? currentFileID { get; set; } = null;
}