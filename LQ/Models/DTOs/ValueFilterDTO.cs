using System.ComponentModel.DataAnnotations;
using System.Net;

namespace LQ.Models.DTOs;

public class ValueFilterDTO
{
    public bool IsPartialMatch { get; set; } = false;
    public bool IsNot { get; set; } = false;
    [Required]
    public int FieldIndex { get; set; }
    [Required] 
    public List<String> FilteringValues { get; set; } = new List<string>();
}