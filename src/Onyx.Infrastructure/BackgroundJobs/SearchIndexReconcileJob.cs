using System.Globalization;
using Onyx.Application.Jobs;
using Onyx.Application.News;
using Onyx.Application.Search;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// Keeps the search index complete: events re-index pages and news items as they change, and this job (every 30 minutes,
/// under the scheduler lease) fills the index the first time and heals anything an event missed. Bounded per run; idempotent.
/// </summary>
public sealed class SearchIndexReconcileJob(SearchIndexer indexer, NewsSearchIndexer news) : IScheduledJob
{
    public const string JobName = "search-index-reconcile";

    public string Name => JobName;

    public string DefaultSchedule => "every:30m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pages = await indexer.ReconcileAsync(cancellationToken).ConfigureAwait(false);
        var items = await news.ReconcileAsync(cancellationToken).ConfigureAwait(false);
        return pages + items == 0
            ? JobOutcome.Skipped("The search index is up to date.")
            : JobOutcome.Succeeded(string.Create(CultureInfo.InvariantCulture, $"Re-indexed {pages} page(s) and {items} news item(s)."));
    }
}
