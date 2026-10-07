namespace Onyx.Application.News;

/// <summary>
/// Technical bounds of the news pipeline (registered by the host from configuration). None of these is a business fact.
/// </summary>
/// <param name="MaxSourcesPerRun">Sources fetched per ingestion run (the rest wait for the next run).</param>
/// <param name="MaxEntriesPerSource">Feed entries considered per source per run.</param>
/// <param name="MaxSummariesPerRun">Summary requests per run across all sources.</param>
/// <param name="SummaryTimeout">Time limit per summary request; a slower answer leaves the item held.</param>
/// <param name="RunBudget">After this long, no further source is started in the same run.</param>
/// <param name="SimilarWindow">Items with the same normalized title within this window are treated as the same story.</param>
public sealed record NewsSettings(
    int MaxSourcesPerRun, int MaxEntriesPerSource, int MaxSummariesPerRun, TimeSpan SummaryTimeout, TimeSpan RunBudget, TimeSpan SimilarWindow)
{
    public static NewsSettings Default { get; } = new(10, 50, 20, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(4), TimeSpan.FromHours(72));
}
