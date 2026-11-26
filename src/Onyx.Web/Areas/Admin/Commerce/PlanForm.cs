namespace Onyx.Web.Areas.Admin.Commerce;

/// <summary>A plan. The price is entered by staff (invariant decimal) and is public only when marked so.</summary>
public sealed class PlanForm
{
    public Guid? PlanId { get; set; }

    public string? Code { get; set; }

    public string? Price { get; set; }

    public string? Currency { get; set; }

    public string? BillingPeriod { get; set; }

    public bool IsPricePublic { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public string? NameAr { get; set; }

    public string? DescriptionAr { get; set; }

    public string? NameEn { get; set; }

    public string? DescriptionEn { get; set; }

    public uint Version { get; set; }
}
