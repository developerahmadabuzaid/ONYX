using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Localization;
using Onyx.Application.Providers;
using Onyx.Application.Search;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.News;
using Onyx.Domain.Providers;

namespace Onyx.Application.News;

/// <summary>
/// The news pipeline for one allowlisted source (architecture.md §17.1): fetch (SSRF-safe, through <see cref="INewsFeedReader"/>)
/// → normalize (canonical URL, title, publisher, source time) → deduplicate (canonical URL; normalized title within a window)
/// → classify (rules) → summary from the external service when connected → auto-publish only when every condition holds,
/// otherwise hold. The article text is processed transiently and never stored.
/// <para>
/// Each source is handled in its own scope and transaction by the caller, so one failing source cannot affect another; a
/// repeated run finds every URL already known and creates nothing (idempotent).
/// </para>
/// </summary>
public sealed class NewsIngestionService(
    INewsSourceRepository sources,
    INewsItemRepository items,
    INewsCategoryRepository categories,
    INewsFeedReader feeds,
    INewsSummaryService summaries,
    NewsSlugAllocator slugs,
    IProviderConnectionService providers,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    NewsSettings settings,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<Guid>> ListDueSourcesAsync(CancellationToken cancellationToken) =>
        sources.ListDueIdsAsync(timeProvider.GetUtcNow(), settings.MaxSourcesPerRun, cancellationToken);

    public async Task<SourceIngestionResult> IngestSourceAsync(Guid sourceId, NewsSummaryBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var source = await sources.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (source is null || !source.IsDue(now))
        {
            return SourceIngestionResult.NotDue;
        }

        var read = await feeds.ReadAsync(new Uri(source.FeedUrl), source.FeedFormat, settings.MaxEntriesPerSource, cancellationToken).ConfigureAwait(false);
        if (read.IsFailure)
        {
            // Only the safe code and message (never a URL or response content) is kept.
            source.RecordFailure(timeProvider.GetUtcNow(), $"{read.Error!.Code}: {read.Error.Message}");
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SourceIngestionResult.Failure;
        }

        var allCategories = await categories.ListAsync(cancellationToken).ConfigureAwait(false);
        var languages = SupportedLanguages.All.Order(StringComparer.Ordinal).ToList();
        var candidates = new List<(FeedEntry Entry, string Canonical, string Hash)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int skipped = 0, duplicates = 0;
        foreach (var entry in read.Value.Take(settings.MaxEntriesPerSource))
        {
            var link = entry.Link is null ? null : NewsUrl.Check(entry.Link, requireHttps: false);
            if (link is null || link.IsFailure || NewsItem.CleanText(entry.Title).Length == 0)
            {
                skipped++;
                continue;
            }

            var canonical = NewsUrl.Canonicalize(link.Value);
            var hash = NewsUrl.Hash(canonical);
            if (!seen.Add(hash))
            {
                duplicates++;
                continue;
            }

            candidates.Add((entry, canonical, hash));
        }

        var existing = await items.ExistingUrlHashesAsync(candidates.Select(candidate => candidate.Hash).ToList(), cancellationToken).ConfigureAwait(false);
        var clusters = new Dictionary<string, Guid>(StringComparer.Ordinal);
        int created = 0, autoPublished = 0, held = 0;
        foreach (var (entry, canonical, hash) in candidates)
        {
            if (existing.Contains(hash))
            {
                duplicates++;
                continue;
            }

            var clusterKey = ClusterKey(entry.Title);
            var similar = clusterKey.Length == 0
                ? null
                : clusters.TryGetValue(clusterKey, out var sameRun)
                    ? sameRun
                    : await items.FindRecentInClusterAsync(clusterKey, source.LanguageCode, now - settings.SimilarWindow, cancellationToken).ConfigureAwait(false);
            var categoryId = NewsClassifier.Classify(allCategories, source.DefaultCategoryId, entry);
            var ingested = NewsItem.Ingest(source, canonical, entry.Title, entry.PublishedAtUtc, categoryId, clusterKey, similar, now);
            if (ingested.IsFailure)
            {
                skipped++;
                continue;
            }

            var item = ingested.Value;
            items.Add(item);
            if (clusterKey.Length > 0)
            {
                clusters[clusterKey] = item.Id;
            }

            created++;
            if (await SummarizeAsync(item, source, entry, languages, budget, cancellationToken).ConfigureAwait(false))
            {
                autoPublished++;
            }
            else
            {
                held++;
            }
        }

        source.RecordSuccess(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SourceIngestionResult(true, false, created, duplicates, skipped, autoPublished, held);
    }

    /// <summary>Records what the summary service did in this run (once per run; its own transaction).</summary>
    public async Task ReportSummaryServiceAsync(NewsSummaryBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (budget.NotConfigured)
        {
            await providers.ReportNotConfiguredAsync(summaries.ProviderKey, ProviderCategory.TextGeneration, cancellationToken).ConfigureAwait(false);
        }
        else if (budget.LastFailureCode is { } failure)
        {
            await providers.ReportFailureAsync(summaries.ProviderKey, ProviderCategory.TextGeneration, failure, cancellationToken).ConfigureAwait(false);
        }
        else if (budget.AnySucceeded)
        {
            await providers.ReportSuccessAsync(summaries.ProviderKey, ProviderCategory.TextGeneration, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string ClusterKey(string title)
    {
        var key = SearchTextNormalizer.Normalize(title);
        return key.Length > NewsItem.ClusterKeyMaxLength ? key[..NewsItem.ClusterKeyMaxLength] : key;
    }

    /// <summary>Asks the external service for summaries; true when the item was published automatically.</summary>
    private async Task<bool> SummarizeAsync(
        NewsItem item, NewsSource source, FeedEntry entry, IReadOnlyList<string> languages, NewsSummaryBudget budget, CancellationToken cancellationToken)
    {
        if (!budget.TryTake())
        {
            return false; // Held: SummaryUnavailable (or SimilarItem), set at ingestion.
        }

        var request = new NewsSummaryRequest(
            item.Id, source.Name, source.LanguageCode, item.OriginalTitle, item.OriginalUrl, item.SourcePublishedAtUtc, entry.Excerpt, languages,
            NewsItemTranslation.TitleMaxLength, NewsItemTranslation.SummaryMinLength, NewsItemTranslation.SummaryMaxLength);
        var answer = await RequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (answer.IsFailure)
        {
            if (answer.Error!.Code == NewsSummaryErrors.NotConfigured)
            {
                budget.RecordNotConfigured();
            }
            else
            {
                budget.RecordFailure(answer.Error.Code);
            }

            return false;
        }

        budget.RecordSuccess();
        var checks = NewsSummaryChecks.Verify(answer.Value, languages, item.OriginalTitle, entry.Excerpt);
        if (checks.IsFailure)
        {
            Hold(item, NewsHoldReason.SummaryRejected);
            return false;
        }

        var newSlugs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var text in answer.Value.Texts)
        {
            if (await slugs.AllocateAsync(text.LanguageCode, text.Title, item.Id, cancellationToken).ConfigureAwait(false) is { } slug)
            {
                newSlugs[text.LanguageCode] = slug;
            }
        }

        if (item.ApplyGeneratedSummaries(answer.Value.Texts, summaries.ProviderKey, newSlugs).IsFailure)
        {
            Hold(item, NewsHoldReason.SummaryRejected);
            return false;
        }

        if (item.SimilarToItemId is not null)
        {
            return false; // Held: SimilarItem — an editor decides.
        }

        if (item.AutoPublish(timeProvider.GetUtcNow(), source, languages).IsFailure)
        {
            Hold(item, NewsHoldReason.AutoPublishOff);
            return false;
        }

        auditTrail.Record(
            AuditAction.NewsItemAutoPublished, AuditCategory.Publishing, nameof(NewsItem), item.Id.ToString(), new { sourceId = source.Id, provider = summaries.ProviderKey });
        return true;
    }

    /// <summary>The external call, bounded in time; any failure of the service becomes an expected result (the item stays held).</summary>
    private async Task<Result<NewsSummaryDraft>> RequestAsync(NewsSummaryRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.SummaryTimeout);
        try
        {
            return await summaries.SummarizeAsync(request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unavailable(NewsSummaryErrors.Timeout, "The summary service did not answer in time.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error.Unavailable(NewsSummaryErrors.Failed, "The summary service failed.");
        }
    }

    private static void Hold(NewsItem item, NewsHoldReason reason)
    {
        if (item.SimilarToItemId is null)
        {
            item.KeepHeld(reason);
        }
    }
}
