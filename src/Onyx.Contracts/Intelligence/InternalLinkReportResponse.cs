using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>Computed from live content only. Changes to published pages are proposed for review, never applied automatically.</summary>
public sealed record InternalLinkReportResponse(
    IReadOnlyList<LinkIssueResponse> BrokenLinks,
    IReadOnlyList<OrphanContentResponse> Orphans,
    IReadOnlyList<LinkSuggestionResponse> Suggestions,
    IReadOnlyList<RepeatedAnchorResponse> RepeatedAnchors,
    IReadOnlyList<LinkIssueResponse> ExcessiveLinks);
