using Onyx.Domain.News;

namespace Onyx.Application.News;

public interface INewsCategoryRepository
{
    /// <summary>All categories with their translations, in sort order.</summary>
    Task<IReadOnlyList<NewsCategory>> ListAsync(CancellationToken cancellationToken);

    Task<NewsCategory?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> KeyExistsAsync(string key, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>A category (other than <paramref name="exceptId"/>) already uses this slug in this language.</summary>
    Task<bool> SlugTakenAsync(string languageCode, string slug, Guid? exceptId, CancellationToken cancellationToken);

    void Add(NewsCategory category);

    void SetExpectedVersion(NewsCategory category, uint version);
}
