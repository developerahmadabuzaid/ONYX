using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Localization;
using Onyx.Contracts.Common;
using Onyx.Contracts.News;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// Editorial work on items (architecture.md §17.3): review, write or edit Onyx's headline and summary, publish, hide,
/// unpublish, delete, recategorize and mark indexable. Publishing always requires a complete summary in every language;
/// every action is audited and guarded by the item's concurrency version.
/// </summary>
public sealed class NewsAdministrationService(
    INewsItemRepository items,
    INewsCategoryRepository categories,
    NewsSlugAllocator slugs,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("News.ItemNotFound", "News item not found.");

    public async Task<Result<PagedResult<NewsItemListItemResponse>>> ListAsync(NewsItemListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        NewsItemStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!EnumParsing.TryParse<NewsItemStatus>(query.Status, out var parsed))
            {
                return Error.Validation("News.InvalidStatus", "Unknown item status.");
            }

            status = parsed;
        }

        var page = await items.ListAsync(status, query.SourceId, query.CategoryId, Math.Max(query.Page, 1), Math.Clamp(query.PageSize, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<NewsItemListItemResponse>(page.Items.Select(NewsMapping.ToListItem).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<NewsItemResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await items.GetAsync(id, cancellationToken).ConfigureAwait(false) is { } item ? NewsMapping.ToResponse(item) : NotFound;

    public async Task<Result<NewsItemResponse>> EditTranslationAsync(Guid id, string languageCode, UpdateNewsTranslationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return Error.Validation("News.InvalidLanguage", "Unsupported language.");
        }

        var item = await items.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return NotFound;
        }

        items.SetExpectedVersion(item, request.Version);
        string? slug = null;
        if (item.FirstPublishedAtUtc is null)
        {
            // Before the item was ever public its slug follows the headline; afterwards the URL never changes.
            slug = await slugs.AllocateAsync(languageCode, request.Title ?? string.Empty, item.Id, cancellationToken).ConfigureAwait(false);
            if (slug is null)
            {
                return Error.Conflict("News.SlugUnavailable", "No free URL could be derived from this headline; change it slightly.");
            }
        }

        var edited = item.EditTranslation(languageCode, request.Title, request.Summary, slug);
        if (edited.IsFailure)
        {
            return edited.Error!;
        }

        auditTrail.Record(AuditAction.NewsItemEdited, AuditCategory.Content, nameof(NewsItem), item.Id.ToString(), new { language = languageCode });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(item);
    }

    public Task<Result<NewsItemResponse>> PublishAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, AuditAction.NewsItemPublished, AuditCategory.Publishing,
            item => item.Publish(timeProvider.GetUtcNow(), SupportedLanguages.All), cancellationToken);

    public Task<Result<NewsItemResponse>> HideAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, AuditAction.NewsItemHidden, AuditCategory.Publishing, item => item.Hide(), cancellationToken);

    public Task<Result<NewsItemResponse>> UnpublishAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, AuditAction.NewsItemUnpublished, AuditCategory.Publishing, item => item.Unpublish(), cancellationToken);

    public Task<Result<NewsItemResponse>> DeleteAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, AuditAction.NewsItemDeleted, AuditCategory.Publishing, item => item.Delete(), cancellationToken);

    public Task<Result<NewsItemResponse>> SetIndexingAsync(Guid id, SetNewsIndexingRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request?.Version, AuditAction.NewsItemIndexingChanged, AuditCategory.Content, item => item.SetIndexable(request!.Indexable), cancellationToken);

    public async Task<Result<NewsItemResponse>> ChangeCategoryAsync(Guid id, ChangeNewsCategoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("News.CategoryNotFound", "Choose an existing category.");
        }

        return await ChangeAsync(id, request.Version, AuditAction.NewsItemEdited, AuditCategory.Content, item =>
        {
            item.ChangeCategory(request.CategoryId);
            return Result.Success();
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<NewsItemResponse>> ChangeAsync(
        Guid id, uint? version, AuditAction action, AuditCategory category, Func<NewsItem, Result> change, CancellationToken cancellationToken)
    {
        if (version is not { } expected)
        {
            return Error.Validation("News.VersionRequired", "Reload the item and try again.");
        }

        var item = await items.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return NotFound;
        }

        items.SetExpectedVersion(item, expected);
        var changed = change(item);
        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        auditTrail.Record(action, category, nameof(NewsItem), item.Id.ToString(), new { status = item.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(item);
    }
}
