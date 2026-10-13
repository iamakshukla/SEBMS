using System.ComponentModel.DataAnnotations;

namespace SEBMS.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class RegisterViewModel
{
    [Display(Name = "Name")] [StringLength(100)] public string? DisplayName { get; set; }
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")] public string ConfirmPassword { get; set; } = "";
}
