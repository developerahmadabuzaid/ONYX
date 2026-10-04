using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Role"/>: Pillar or Supporting.</summary>
public sealed record SetClusterMemberRequest(Guid ContentItemId, string Role);
