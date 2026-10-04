using Onyx.Domain.Common;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// Aggregated performance of one content translation for one period, written only by a connected provider's sync job
/// (GA4, Search Console, …). There is no manual entry path: absent snapshots mean "insufficient data", never zero.
/// Heatmaps or session behaviour would come from a separate behaviour-analytics provider, not from web analytics.
/// </summary>
public sealed class ContentPerformanceSnapshot : Entity
{
    private ContentPerformanceSnapshot()
    {
        LanguageCode = string.Empty;
        Metrics = new Dictionary<string, double>();
    }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public EvidenceSource Source { get; private set; }

    public DateTimeOffset PeriodStartUtc { get; private set; }

    public DateTimeOffset PeriodEndUtc { get; private set; }

    public IReadOnlyDictionary<string, double> Metrics { get; private set; }

    public DateTimeOffset CollectedAtUtc { get; private set; }

    public static Result<ContentPerformanceSnapshot> Collect(
        Guid contentItemId, string languageCode, EvidenceSource source, DateTimeOffset periodStartUtc, DateTimeOffset periodEndUtc,
        IReadOnlyDictionary<string, double> metrics, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        var evidence = new EvidenceItem(source, "snapshot", now, periodStartUtc, periodEndUtc, metrics);
        if (!EvidenceItem.IsDataProvider(source) || metrics.Count == 0 || evidence.Problem() is not null)
        {
            return Error.Validation("Snapshot.Invalid", "Snapshots come only from connected data providers, with a period and metrics.");
        }

        return new ContentPerformanceSnapshot
        {
            ContentItemId = contentItemId,
            LanguageCode = languageCode,
            Source = source,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            Metrics = new Dictionary<string, double>(metrics),
            CollectedAtUtc = now,
        };
    }
}
