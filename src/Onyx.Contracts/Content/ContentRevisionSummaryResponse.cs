using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ContentRevisionSummaryResponse(
    Guid Id, int Number, string Kind, string? Note, DateTimeOffset CreatedAtUtc, Guid? CreatedById, bool IsLive, Guid? RestoredFromRevisionId);
