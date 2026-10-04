namespace Onyx.Domain.Content;

/// <summary>An industry may exist only as a taxonomy term (tags, filters) without an indexable page.</summary>
public enum ContentPresentation
{
    Page = 0,
    TaxonomyOnly = 1,
}
