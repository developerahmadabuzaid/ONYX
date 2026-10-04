namespace Onyx.Domain.Content;

/// <summary>
/// Structured data an editor selects for a translation. It is emitted only when the visible content supports it; the quality
/// gate blocks types that would misdescribe the page (e.g. FAQ markup without visible FAQs). Stored as text.
/// </summary>
[Flags]
public enum StructuredDataTypes
{
    None = 0,
    WebPage = 1,
    BreadcrumbList = 2,
    Service = 4,
    FaqPage = 8,
}
