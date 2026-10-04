namespace Onyx.Infrastructure.Seo;

/// <summary>Bound from <c>Seo</c>.</summary>
public sealed class SeoOptions
{
    public const string SectionName = "Seo";

    /// <summary>Owner switch for Organization structured data; off until the Brand Kit logo and website are verified.</summary>
    public bool OrganizationStructuredDataEnabled { get; set; }
}
