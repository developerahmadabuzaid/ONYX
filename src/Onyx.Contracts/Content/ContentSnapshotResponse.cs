using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record ContentSnapshotResponse(
    string Type,
    Guid? ParentId,
    string? Key,
    string Presentation,
    int SortOrder,
    Guid? PrimaryCtaId,
    Guid? FeaturedImageId,
    IReadOnlyList<ContentTranslationResponse> Translations);
