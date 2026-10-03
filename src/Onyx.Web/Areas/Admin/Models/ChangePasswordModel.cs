using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Areas.Admin.Models;

public sealed class ChangePasswordModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.CurrentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Validation.PasswordLength")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Password.Mismatch")]
    [DataType(DataType.Password)]
    [Display(Name = "Field.ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
