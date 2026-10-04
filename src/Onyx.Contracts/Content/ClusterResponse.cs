using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Observations"/>: coverage facts derived from the cluster graph (e.g. no pillar page, supporting pages not live).</summary>
public sealed record ClusterResponse(
    Guid Id, string Name, string? Description, bool IsActive, IReadOnlyList<ClusterMemberResponse> Members, IReadOnlyList<string> Observations, uint Version);
