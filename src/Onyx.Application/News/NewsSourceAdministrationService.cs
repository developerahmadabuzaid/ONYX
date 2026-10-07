using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Localization;
using Onyx.Contracts.News;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// The source allowlist (<c>NewsSources.Manage</c>). Sources start blocked; allowing one requires the reviewed licensing/terms
/// note. Audit entries never contain the feed URL (it may carry a publisher-issued key).
/// </summary>
public sealed class NewsSourceAdministrationService(
    INewsSourceRepository sources,
    INewsCategoryRepository categories,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly Error NotFound = Error.NotFound("News.SourceNotFound", "News source not found.");

    public async Task<IReadOnlyList<NewsSourceResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await sources.ListAsync(cancellationToken).ConfigureAwait(false)).Select(NewsMapping.ToResponse).ToList();

    public async Task<Result<NewsSourceResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await sources.GetAsync(id, cancellationToken).ConfigureAwait(false) is { } source ? NewsMapping.ToResponse(source) : NotFound;

    public async Task<Result<NewsSourceResponse>> CreateAsync(SaveNewsSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var checkedInput = await CheckAsync(request, null, cancellationToken).ConfigureAwait(false);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error!;
        }

        var created = NewsSource.Create(
            request.Name, request.HomepageUrl, request.FeedUrl, checkedInput.Value, request.LanguageCode, request.DefaultCategoryId,
            request.FetchIntervalMinutes, request.LicensingNote, request.AutoPublish);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        sources.Add(created.Value);
        auditTrail.Record(AuditAction.NewsSourceCreated, AuditCategory.Content, nameof(NewsSource), created.Value.Id.ToString(), new { name = created.Value.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(created.Value);
    }

    public async Task<Result<NewsSourceResponse>> UpdateAsync(Guid id, SaveNewsSourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return Error.Validation("News.VersionRequired", "Reload the source and try again.");
        }

        var source = await sources.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return NotFound;
        }

        var checkedInput = await CheckAsync(request, id, cancellationToken).ConfigureAwait(false);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error!;
        }

        sources.SetExpectedVersion(source, version);
        var updated = source.Update(
            request.Name, request.HomepageUrl, request.FeedUrl, checkedInput.Value, request.LanguageCode, request.DefaultCategoryId,
            request.FetchIntervalMinutes, request.LicensingNote, request.AutoPublish);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        auditTrail.Record(AuditAction.NewsSourceUpdated, AuditCategory.Content, nameof(NewsSource), source.Id.ToString(), new { status = source.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(source);
    }

    public Task<Result<NewsSourceResponse>> AllowAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request, AuditAction.NewsSourceAllowed, source => source.Allow(timeProvider.GetUtcNow()), cancellationToken);

    public Task<Result<NewsSourceResponse>> BlockAsync(Guid id, NewsVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request, AuditAction.NewsSourceBlocked, source =>
        {
            source.Block();
            return Result.Success();
        }, cancellationToken);

    private async Task<Result<NewsSourceResponse>> ChangeAsync(
        Guid id, NewsVersionRequest request, AuditAction action, Func<NewsSource, Result> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = await sources.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return NotFound;
        }

        sources.SetExpectedVersion(source, request.Version);
        var changed = change(source);
        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        auditTrail.Record(action, AuditCategory.Content, nameof(NewsSource), source.Id.ToString(), new { name = source.Name });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return NewsMapping.ToResponse(source);
    }

    private async Task<Result<NewsFeedFormat>> CheckAsync(SaveNewsSourceRequest request, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (!EnumParsing.TryParse<NewsFeedFormat>(request.FeedFormat, out var format))
        {
            return Error.Validation("News.InvalidFeedFormat", "The feed format is Rss or Atom.");
        }

        if (request.LanguageCode is null || !SupportedLanguages.All.Contains(request.LanguageCode))
        {
            return Error.Validation("News.InvalidLanguage", "Choose a site language for the source.");
        }

        if (!await categories.ExistsAsync(request.DefaultCategoryId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Validation("News.CategoryNotFound", "Choose an existing category.");
        }

        var feed = NewsUrl.Parse(request.FeedUrl, requireHttps: true);
        if (feed.IsSuccess && await sources.FeedUrlExistsAsync(feed.Value.AbsoluteUri, exceptId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("News.FeedExists", "Another source already uses this feed.");
        }

        return format;
    }
}
