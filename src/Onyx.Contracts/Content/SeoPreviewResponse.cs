using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>How the page will appear to search engines, plus the current quality-gate findings.</summary>
public sealed record SeoPreviewResponse(
    string Path,
    string TitleTag,
    string? MetaDescription,
    string Robots,
    string? CanonicalPath,
    IReadOnlyList<HeadingOutlineResponse> Headings,
    IReadOnlyList<ImageCheckResponse> Images,
    int FaqCount,
    IReadOnlyList<string> StructuredData,
    QualityReportResponse Quality);
