using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Proof;

/// <summary><see cref="Kind"/>: Capability, Metric or Testimonial. Any change returns the item to an unverified draft.</summary>
public sealed record SaveProofItemRequest(
    Guid ContentItemId,
    string Kind,
    IReadOnlyList<LocalizedTextDto> Texts,
    string? Value,
    DateOnly? AsOf,
    string? AttributionName,
    string? AttributionRole,
    string? AttributionOrganization,
    int SortOrder,
    uint? Version);
