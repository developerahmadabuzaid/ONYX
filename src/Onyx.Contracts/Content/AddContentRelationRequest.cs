using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Kind"/>: Related or ServiceIndustry.</summary>
public sealed record AddContentRelationRequest(Guid ToContentItemId, string Kind, int Position, string? Note);
