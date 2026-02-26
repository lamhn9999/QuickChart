using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.JavaScript;
using LQ.Models.AnalystModels;

namespace LQ.Models.DTOs;

public class RegisterDTO
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(Analyst.NameUpperBound, MinimumLength = Analyst.NameLowerBound, ErrorMessage = "Username must be between {2} and {1} characters long.")]
    [RegularExpression(@"^[a-zA-Z0-9_\.]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and dots.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")] [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = String.Empty;
    
    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}