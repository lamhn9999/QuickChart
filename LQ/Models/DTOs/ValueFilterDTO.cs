using System.ComponentModel.DataAnnotations;
using System.Net;
using LQ.Models.TableModels;

namespace LQ.Models.DTOs;

public class ValueFilterDTO
{
    public bool IsPartialMatch { get; init; } = false;
    public bool IsNot { get; init; } = false;
    public bool IsNumber { get; init; } = false;
    [Required]
    public int FieldIndex { get; init; }
    public List<String> FilteringValues { get; init; } = new List<string>();
    public (int leftBound, int rightBound)? IntBound;
    public (double leftBound, double rightBound)? DoubleBound;
    public (DateTime leftBound, DateTime rightBound)? DateTimeBound;
}
