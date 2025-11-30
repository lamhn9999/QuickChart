namespace LQ.Models.DTOs;

public class ColumnModel
{
    public required FieldModel Field;
    public required List<ValueModel> Values { get; set; }

    public bool AddValue(string? newValueString)
    {
        if (Field.FieldType is null) return false;
        if (newValueString is null)
        {
            Values.Add(new ValueModel{Id = Values.Count});
        }
        else Values.Add(new ValueModel{Value = Convert.ChangeType(newValueString, Field.FieldType), Id = Values.Count});

        return true;
    }
}