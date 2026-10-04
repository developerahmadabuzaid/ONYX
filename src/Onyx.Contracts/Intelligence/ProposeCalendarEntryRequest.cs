using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>An editor proposal. A date, if given, is recorded as the editor's manual choice (never as a data-backed recommendation).</summary>
public sealed record ProposeCalendarEntryRequest(
    string Topic,
    string LanguageCode,
    string? TargetMarket,
    Guid? TargetServiceId,
    Guid? ClusterId,
    string? Keyword,
    string? SearchIntent,
    string? Reason,
    IReadOnlyList<Guid>? SupportingContentIds,
    DateTimeOffset? ProposedPublishAt);
