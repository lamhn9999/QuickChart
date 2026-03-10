using System.ComponentModel.DataAnnotations;
using System.Net;
using LQ.Models.TableModels;

namespace LQ.Models.DTOs;

public class ValueFilterDTO
{
    public bool IsPartialMatch { get; set; } = false;
    public bool IsNot { get; set; } = false;
    public bool IsNumber { get; set; } = false;
    [Required]
    public int FieldIndex { get; set; }
    [Required] 
    public List<String> FilteringValues { get; set; } = new List<string>();
    public void MatchFieldProperty(Type fieldType, List<Category> fieldCategories)
    {
        switch (fieldType)
        {
            case var t when t == typeof(string) && fieldCategories.Contains(Category.Identifier):
                IsPartialMatch = true;
                break;

            case var t when (t == typeof(int) || t== typeof(double)) && !fieldCategories.Contains(Category.Categorical):
                IsNumber = true;
                break;
        }
    }
}
