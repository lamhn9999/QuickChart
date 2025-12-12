using System.ComponentModel.DataAnnotations.Schema;

namespace LQ.Models.TableModels;

public enum Category
{
    Categorical, Numeric, Identifier
}

public class Field
{
    public int ID { get; set; }
    public string Name { get; set; }
    public int Index { get; set; }
    
    public string? TypeName { get; set; }   
    [NotMapped]
    public Type? Type
    {
        get => string.IsNullOrEmpty(TypeName) ? null : Type.GetType(TypeName); 
        set => TypeName = value?.AssemblyQualifiedName;
    }
    public List<Category> Categories { get; set; }
    
    public int CSVFileID { get; set; }
    public CSVFile CSVFile { get; set; }
}