namespace Onyx.Web.Areas.Admin.Commerce;

/// <summary>Product details in both languages (written separately, never machine-translated).</summary>
public sealed class ProductForm
{
    public int? TrialDays { get; set; }

    public string? DemoKind { get; set; }

    public string? DemoUrl { get; set; }

    public int SortOrder { get; set; }

    public string? NameAr { get; set; }

    public string? SlugAr { get; set; }

    public string? SummaryAr { get; set; }

    public string? DescriptionAr { get; set; }

    public string? NameEn { get; set; }

    public string? SlugEn { get; set; }

    public string? SummaryEn { get; set; }

    public string? DescriptionEn { get; set; }

    public uint Version { get; set; }
}
