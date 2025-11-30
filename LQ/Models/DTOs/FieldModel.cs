namespace LQ.Models.DTOs;
public class FieldModel
{
    public enum FieldCategory
    {
        Numeric, 
        Binary,
        Boolean,
        Date,
        Time,
        Categorical,
        Text,
    }
    public required string FieldName { get; set; }
    public Type? FieldType { get; set; }
    public required List<FieldCategory> FieldCategories { get; set; } 
}