using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record RecommendationResponse(
    Guid Id,
    Guid? ContentItemId,
    string? LanguageCode,
    Guid? ClusterId,
    string Kind,
    string Origin,
    string Status,
    string Basis,
    string Title,
    string DetectedIssue,
    string? Reasoning,
    IReadOnlyList<ProposedChangeDto> ProposedChanges,
    IReadOnlyList<EvidenceDto> Evidence,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNote,
    DateTimeOffset? AppliedAtUtc,
    IReadOnlyList<ObservationResponse> Observations,
    uint Version);
