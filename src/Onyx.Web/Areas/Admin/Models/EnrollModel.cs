using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Areas.Admin.Models;

public sealed class EnrollModel
{
    public string SharedKey { get; set; } = string.Empty;

    public string AuthenticatorUri { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation.Required")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "TwoFactor.Invalid")]
    [Display(Name = "Field.Code")]
    public string Code { get; set; } = string.Empty;
}
