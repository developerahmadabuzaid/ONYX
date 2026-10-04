using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record CalendarEntryResponse(
    Guid Id,
    string Topic,
    string LanguageCode,
    string? TargetMarket,
    Guid? TargetServiceId,
    Guid? ClusterId,
    string? Keyword,
    string SearchIntent,
    string? Reason,
    IReadOnlyList<Guid> SupportingContentIds,
    DateTimeOffset? ProposedPublishAtUtc,
    string TimingBasis,
    IReadOnlyList<EvidenceDto> Evidence,
    string Origin,
    string Status,
    Guid? LinkedContentItemId,
    string? DecisionNote,
    DateTimeOffset CreatedAtUtc,
    uint Version);
