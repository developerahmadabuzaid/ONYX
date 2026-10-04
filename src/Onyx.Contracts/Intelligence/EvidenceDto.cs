using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary>Provenance. <see cref="Metrics"/> appear only when a connected data provider supplied them, with the period they describe.</summary>
public sealed record EvidenceDto(
    string Source,
    string Description,
    DateTimeOffset? ObservedAtUtc,
    DateTimeOffset? PeriodStartUtc,
    DateTimeOffset? PeriodEndUtc,
    IReadOnlyDictionary<string, double>? Metrics);
