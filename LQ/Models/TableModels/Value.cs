using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace LQ.Models.TableModels;

public class Value
{
    public string ValueString { get; set; }
    [NotMapped]
    public object ValueCasted { get; set; }
    
    public int RowIndex { get; set; }
    public bool Existance { get; set; }
    
    public int FieldID { get; set; }
    public Field Field { get; set; }
}