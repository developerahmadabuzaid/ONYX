using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Title"/> is the capability name, the metric label or the quote.</summary>
public sealed record PublicProofItemResponse(
    string Title, string? Description, string? Value, DateOnly? AsOf, string? AttributionName, string? AttributionRole, string? AttributionOrganization);
