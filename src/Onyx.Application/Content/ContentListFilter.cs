using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Admin list filter. <see cref="AllowedTypes"/> restricts results to the types the admin may view.</summary>
public sealed record ContentListFilter(
    IReadOnlyCollection<ContentType> AllowedTypes,
    ContentType? Type,
    ContentStatus? Status,
    bool? IsLive,
    bool NeedsUrlDecision,
    Guid? ParentId,
    string? Search,
    int Page,
    int PageSize);
