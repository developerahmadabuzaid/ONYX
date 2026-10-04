using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ClusterMemberResponse(Guid ContentItemId, string Role, IReadOnlyDictionary<string, string> Titles, bool IsLive);
