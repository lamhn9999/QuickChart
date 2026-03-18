using System.ComponentModel.DataAnnotations.Schema;

namespace LQ.Models.TableModels;

public enum Category
{
    Categorical, Numeric, Identifier
}

public class Field
{
    public const double IDENTIFIER_THRESHOLD = 0.9;
    public const double CATEGORICAL_THRESHOLD = 0.1;
    public const double IDENTIFIER_LIST_THRESHOLD = 0.6;
    public int ID { get; private set; }
    public string? Name { get; init; }
    public int Index { get; init; }
    
    private string? _typeName;
    public string? TypeName 
    { 
        get => _typeName; 
        set 
        {
            _typeName = value;
            _type = null;
        }
    }   

    private Type? _type;
    [NotMapped]
    public Type? Type
    {
        get
        {
            if (_type == null && !string.IsNullOrEmpty(_typeName))
            {
                _type = System.Type.GetType(_typeName);
            }
            return _type;
        }
        set 
        {
            _type = value;
            _typeName = value?.AssemblyQualifiedName;
        }
    }

    public required List<Category> Categories { get; set; } = new();
    public required CSVFile CSVFile { get; set; }
}