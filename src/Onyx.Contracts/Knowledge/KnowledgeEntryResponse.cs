using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Knowledge;

public sealed record KnowledgeEntryResponse(
    Guid Id,
    string Category,
    string Title,
    IReadOnlyList<LocalizedTextDto> Statements,
    string? SourceNote,
    string Status,
    DateTimeOffset? VerifiedAtUtc,
    Guid? VerifiedById,
    DateTimeOffset? UpdatedAtUtc,
    uint Version);
