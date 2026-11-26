using Onyx.Domain.Common;

namespace Onyx.Domain.Products;

/// <summary>One language of a product: name, slug and copy. Arabic and English are written separately, never translated.</summary>
public sealed class ProductTranslation : Entity
{
    public const int NameMaxLength = 120;
    public const int SummaryMaxLength = 300;
    public const int DescriptionMaxLength = 6000;

    private ProductTranslation()
    {
        Language = string.Empty;
        Name = string.Empty;
        Slug = string.Empty;
        Summary = string.Empty;
        Description = string.Empty;
    }

    public Guid ProductId { get; private set; }

    public string Language { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public string Summary { get; private set; }

    /// <summary>Plain text; paragraphs separated by blank lines. Rendered encoded, never as HTML.</summary>
    public string Description { get; private set; }

    internal static ProductTranslation Create(Guid productId, string language) =>
        new() { ProductId = productId, Language = language };

    internal void Set(string name, string slug, string summary, string description)
    {
        Name = name;
        Slug = slug;
        Summary = summary;
        Description = description;
    }
}
