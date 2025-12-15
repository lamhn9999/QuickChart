using System.ComponentModel.DataAnnotations.Schema;

namespace LQ.Models.TableModels;

public enum Category
{
    Categorical, Numeric, Identifier
}

public class Field
{
    public int ID { get; private set; }
    public string? Name { get; set; }
    public int Index { get; set; }
    
    public string? TypeName { get; set; }   
    [NotMapped]
    public Type? Type
    {
        get => string.IsNullOrEmpty(TypeName) ? null : Type.GetType(TypeName); 
        set => TypeName = value?.AssemblyQualifiedName;
    }

    public required List<Category> Categories { get; set; } = new();
    public required CSVFile CSVFile { get; set; }
}