using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace LQ.Models.TableModels;

public class Value
{
    private string? _valueStringFormatted;
    public string? ValueStringFormatted 
    { 
        get => _valueStringFormatted; 
        set 
        {
            _valueStringFormatted = value;
            _isCasted = false;
        }
    }
    public string? ValueString { get; set; }

    private bool _isCasted = false;
    private object? _valueCasted;

    [NotMapped] 
    public object? ValueCasted 
    {
        get
        {
            if (!_isCasted)
            {
                _valueCasted = CastValue(_valueStringFormatted);
                _isCasted = true;
            }
            return _valueCasted;
        }
    }

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
            return ValueStringFormatted?.Split(',').ToList();
        return null;
    }
}