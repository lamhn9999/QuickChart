using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace LQ.Models.TableModels;

public class Value
{
    public string? ValueStringFormatted { get; set; }
    public string? ValueString { get; set; }

    [NotMapped] public object? ValueCasted => CastValue(ValueStringFormatted);

    public int RowIndex { get; set; }
    public bool Existance { get; set; } = true;
    public int FieldID { get; set; }
    public required Field Field { get; set; }

    private object? CastValue(string? value)
    {
        if (String.IsNullOrEmpty(value)) return null;
        if (Field.Type is null) return null;
        if (Field.Type == typeof(int)) return int.Parse(value);
        if (Field.Type == typeof(double)) return double.Parse(value);
        if (Field.Type == typeof(string)) return value;
        if (Field.Type == typeof(bool)) return bool.Parse(value);
        if (Field.Type == typeof(DateTime)) 
            return DateTime.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime().Year;
        if (Field.Type == typeof(List<string>))
            return ValueStringFormatted is null ? null : ValueStringFormatted.Split(',').ToList();
        return null;
    }
}