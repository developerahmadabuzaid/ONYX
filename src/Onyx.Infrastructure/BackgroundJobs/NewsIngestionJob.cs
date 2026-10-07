using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Onyx.Application.Jobs;
using Onyx.Application.News;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>
/// News ingestion (architecture.md §17.1, §21), every 15 minutes under the scheduler lease: due allowlisted sources, each in
/// its own scope and transaction (one bad source never affects another), bounded by sources, entries, summary requests and a
/// time budget. A source that starts failing raises one notification; the run fails (JobRun error and notification) only when
/// every attempted source failed. Idempotent: known URLs are never stored twice.
/// </summary>
public sealed partial class NewsIngestionJob(IServiceScopeFactory scopes, NewsSettings settings, ILogger<NewsIngestionJob> logger) : IScheduledJob
{
    public const string JobName = "news-ingestion";

    public string Name => JobName;

    public string DefaultSchedule => "every:15m";

    public async Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<NewsIngestionService>().ListDueSourcesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (due.Count == 0)
        {
            return JobOutcome.Skipped("No news source is due.");
        }

        var budget = new NewsSummaryBudget(settings.MaxSummariesPerRun);
        var started = Stopwatch.GetTimestamp();
        int attempted = 0, failed = 0, created = 0, published = 0, held = 0, duplicates = 0;
        foreach (var sourceId in due)
        {
            if (Stopwatch.GetElapsedTime(started) > settings.RunBudget)
            {
                break; // The rest stay due and run next time.
            }

            SourceIngestionResult result;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                result = await scope.ServiceProvider.GetRequiredService<NewsIngestionService>().IngestSourceAsync(sourceId, budget, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Unexpected (e.g. storage) failure: this source's transaction was not committed; the others continue.
                LogSourceFailed(logger, exception, sourceId);
                result = SourceIngestionResult.Failure;
            }

            if (!result.Attempted)
            {
                continue;
            }

            attempted++;
            failed += result.Failed ? 1 : 0;
            created += result.NewItems;
            published += result.AutoPublished;
            held += result.Held;
            duplicates += result.Duplicates;
        }

        await using (var scope = scopes.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NewsIngestionService>().ReportSummaryServiceAsync(budget, cancellationToken).ConfigureAwait(false);
        }

        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"Sources {attempted - failed} ok, {failed} failed; items new {created}, auto-published {published}, held {held}, duplicates {duplicates}; summary requests {budget.Requested}.");
        if (attempted > 0 && failed == attempted)
        {
            throw new InvalidOperationException($"Every news source attempted failed. {summary}");
        }

        return attempted == 0 ? JobOutcome.Skipped("No news source was due.") : JobOutcome.Succeeded(summary);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "News ingestion failed for source {SourceId}")]
    private static partial void LogSourceFailed(ILogger logger, Exception exception, Guid sourceId);
}
