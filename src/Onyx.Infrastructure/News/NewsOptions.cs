using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.News;

/// <summary>Bound from <c>News</c>. Technical bounds of the ingestion pipeline (see <see cref="Application.News.NewsSettings"/>).</summary>
public sealed class NewsOptions
{
    public const string SectionName = "News";

    [Range(1, 100)]
    public int MaxSourcesPerRun { get; set; } = 10;

    [Range(1, 200)]
    public int MaxEntriesPerSource { get; set; } = 50;

    [Range(0, 500)]
    public int MaxSummariesPerRun { get; set; } = 20;

    [Range(1, 120)]
    public int SummaryTimeoutSeconds { get; set; } = 30;

    [Range(1, 30)]
    public int RunBudgetMinutes { get; set; } = 4;

    [Range(1, 720)]
    public int SimilarWindowHours { get; set; } = 72;
}
