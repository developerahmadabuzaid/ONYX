namespace Onyx.Web.Areas.Admin.Models;

public sealed class ContentSeoForm
{
    public string? PrimaryKeyword { get; set; }

    /// <summary>Comma- or line-separated.</summary>
    public string? SecondaryKeywords { get; set; }

    public string? SearchIntent { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? CanonicalPath { get; set; }

    public bool Index { get; set; } = true;

    public bool Follow { get; set; } = true;

    public List<string> StructuredData { get; set; } = [];

    public string? OpenGraphTitle { get; set; }

    public string? OpenGraphDescription { get; set; }

    public Guid? OpenGraphImageId { get; set; }

    public string? TargetMarket { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }

    public uint Version { get; set; }
}
