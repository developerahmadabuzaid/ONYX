using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Proof;

public sealed record ProofItemResponse(
    Guid Id,
    Guid ContentItemId,
    string Kind,
    IReadOnlyList<LocalizedTextDto> Texts,
    string? Value,
    DateOnly? AsOf,
    string? AttributionName,
    string? AttributionRole,
    string? AttributionOrganization,
    int SortOrder,
    string Status,
    ProofVerificationResponse? Verification,
    DateTimeOffset? PublishedAtUtc,
    uint Version);
