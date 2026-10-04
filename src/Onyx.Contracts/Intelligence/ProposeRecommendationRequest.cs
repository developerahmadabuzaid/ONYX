using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>An editor's own recommendation. It is recorded as editor judgement; editors cannot attach provider metrics.</summary>
public sealed record ProposeRecommendationRequest(
    string Kind,
    string Title,
    string DetectedIssue,
    string? Reasoning,
    IReadOnlyList<ProposedChangeDto> ProposedChanges,
    Guid? ContentItemId,
    string? LanguageCode,
    Guid? ClusterId);
