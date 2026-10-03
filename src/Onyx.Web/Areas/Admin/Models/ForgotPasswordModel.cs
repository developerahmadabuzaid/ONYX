using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Areas.Admin.Models;

public sealed class ForgotPasswordModel
{
    [Required(ErrorMessage = "Validation.Required")]
    [EmailAddress(ErrorMessage = "Validation.Email")]
    [Display(Name = "Field.Email")]
    public string Email { get; set; } = string.Empty;
}
