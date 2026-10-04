using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Per-language SEO settings. Enum values are strings; <see cref="StructuredData"/> lists WebPage, BreadcrumbList, Service, FaqPage.</summary>
public sealed record SeoProfileDto(
    string? PrimaryKeyword,
    IReadOnlyList<string>? SecondaryKeywords,
    string? SearchIntent,
    string? MetaTitle,
    string? MetaDescription,
    string? CanonicalPath,
    bool Index,
    bool Follow,
    IReadOnlyList<string>? StructuredData,
    string? OpenGraphTitle,
    string? OpenGraphDescription,
    Guid? OpenGraphImageId,
    string? TargetMarket,
    string? Status,
    string? Notes);
