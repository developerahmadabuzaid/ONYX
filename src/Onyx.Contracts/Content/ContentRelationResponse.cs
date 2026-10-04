using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ContentRelationResponse(Guid Id, Guid ToContentItemId, string Kind, int Position, string? Note, IReadOnlyDictionary<string, string> Titles);
