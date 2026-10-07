using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Localization;
using Onyx.Contracts.News;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// News categories (architecture.md §17.3: admin-extensible). Every category needs a name in every site language; slugs are
/// unique among categories and never equal an item slug (both live directly below the hub).
/// </summary>
public sealed class NewsCategoryAdministrationService(
    INewsCategoryRepository categories,
    INewsItemRepository items,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork)
{
    private static readonly Error NotFound = Error.NotFound("News.CategoryNotFound", "News category not found.");

    public async Task<IReadOnlyList<NewsCategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await categories.ListAsync(cancellationToken).ConfigureAwait(false)).Select(NewsMapping.ToResponse).ToList();

    public async Task<Result<NewsCategoryResponse>> CreateAsync(SaveNewsCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await categories.KeyExistsAsync(request.Key?.Trim() ?? string.Empty, null, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("News.CategoryKeyExists", "Another category uses this key.");
        }

        var created = NewsCategory.Create(request.Key, request.SortOrder);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var applied = await ApplyAsync(created.Value, request, cancellationToken).ConfigureAwait(false);
        if (applied.IsFailure)
        {
            return applied.Error!;
        }

        categories.Add(created.Value);
        auditTrail.Record(AuditAction.NewsCategoryCreated, AuditCategory.Content, nameof(NewsCategory), created.Value.Id.ToString(), new { key = created.Value.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(created.Value);
    }

    public async Task<Result<NewsCategoryResponse>> UpdateAsync(Guid id, SaveNewsCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return Error.Validation("News.VersionRequired", "Reload the category and try again.");
        }

        var category = await categories.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return NotFound;
        }

        categories.SetExpectedVersion(category, version);
        var applied = await ApplyAsync(category, request, cancellationToken).ConfigureAwait(false);
        if (applied.IsFailure)
        {
            return applied.Error!;
        }

        auditTrail.Record(AuditAction.NewsCategoryUpdated, AuditCategory.Content, nameof(NewsCategory), category.Id.ToString(), new { key = category.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(category);
    }

    private async Task<Result> ApplyAsync(NewsCategory category, SaveNewsCategoryRequest request, CancellationToken cancellationToken)
    {
        var translations = request.Translations ?? [];
        foreach (var language in SupportedLanguages.All.Order(StringComparer.Ordinal))
        {
            var translation = translations.FirstOrDefault(candidate => candidate.LanguageCode == language);
            if (translation is null)
            {
                return Error.Validation("News.CategoryNameRequired", $"Enter the category name in '{language}'.");
            }

            var set = category.SetTranslation(language, translation.Name, translation.Slug);
            if (set.IsFailure)
            {
                return set;
            }

            var slug = category.Translation(language)!.Slug;
            if (await categories.SlugTakenAsync(language, slug, category.Id, cancellationToken).ConfigureAwait(false)
                || await items.SlugTakenAsync(language, slug, null, cancellationToken).ConfigureAwait(false))
            {
                return Error.Conflict("News.CategorySlugTaken", $"The slug '{slug}' is already used in '{language}'.");
            }
        }

        if (translations.Any(translation => !SupportedLanguages.All.Contains(translation.LanguageCode)))
        {
            return Error.Validation("News.InvalidLanguage", "Only site languages are allowed.");
        }

        category.SetSortOrder(request.SortOrder);
        return category.SetKeywords(NewsClassifier.NormalizeKeywords(request.Keywords));
    }
}
