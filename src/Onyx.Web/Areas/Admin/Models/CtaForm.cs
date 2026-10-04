namespace Onyx.Web.Areas.Admin.Models;

public sealed class CtaForm
{
    public string? Key { get; set; }

    public string? ActionType { get; set; }

    public string? TargetUrl { get; set; }

    public string? LabelAr { get; set; }

    public string? SecondaryAr { get; set; }

    public string? LabelEn { get; set; }

    public string? SecondaryEn { get; set; }

    public bool IsActive { get; set; } = true;
}
