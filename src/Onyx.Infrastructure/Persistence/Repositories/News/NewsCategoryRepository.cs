using Microsoft.EntityFrameworkCore;
using Onyx.Application.News;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class NewsCategoryRepository(ApplicationDbContext dbContext) : INewsCategoryRepository
{
    public async Task<IReadOnlyList<NewsCategory>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.NewsCategories.AsNoTracking()
            .Include(category => category.Translations)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<NewsCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.Include(category => category.Translations).SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.AnyAsync(category => category.Id == id, cancellationToken);

    public Task<bool> KeyExistsAsync(string key, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.NewsCategories.AnyAsync(category => category.Key == key && (exceptId == null || category.Id != exceptId), cancellationToken);

    public Task<bool> SlugTakenAsync(string languageCode, string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.NewsCategoryTranslations.AnyAsync(
            translation => translation.LanguageCode == languageCode && translation.Slug == slug && (exceptId == null || translation.NewsCategoryId != exceptId),
            cancellationToken);

    public void Add(NewsCategory category) => dbContext.NewsCategories.Add(category);

    public void SetExpectedVersion(NewsCategory category, uint version) =>
        dbContext.Entry(category).Property(entry => entry.Version).OriginalValue = version;
}
