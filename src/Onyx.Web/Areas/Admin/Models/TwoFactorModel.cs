using System.ComponentModel.DataAnnotations;

namespace Onyx.Web.Areas.Admin.Models;

public sealed class TwoFactorModel
{
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "TwoFactor.Invalid")]
    [Display(Name = "Field.Code")]
    public string? Code { get; set; }

    [StringLength(64)]
    [Display(Name = "Field.RecoveryCode")]
    public string? RecoveryCode { get; set; }
}
