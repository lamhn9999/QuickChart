using System.ComponentModel.DataAnnotations;
using System.Net;
using LQ.Models.TableModels;

namespace LQ.Models.DTOs;

public enum FilterFieldType
{
    String,
    Int,
    Double,
    DateTime,
    Bool,
    ListString
}

public struct RangeBound<T>
{
    public T leftBound { get; set; }
    public T rightBound { get; set; }
}

public class ValueFilterDTO
{
    public FilterFieldType FieldType { get; init; }
    public bool IsPartialMatch { get; init; } = false;
    public bool IsNot { get; init; } = false;
    [Required]
    public int FieldIndex { get; init; }
    public List<String>? FilteringValues { get; init; }
    public List<int>? FilteringValuesInt { get; init; }
    public List<double>? FilteringValuesDouble { get; init; }
    public RangeBound<int>? IntBound { get; init; }
    public RangeBound<double>? DoubleBound { get; init; }
    public RangeBound<DateTime>? DateTimeBound { get; init; }
}
