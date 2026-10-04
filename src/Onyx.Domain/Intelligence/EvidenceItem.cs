namespace Onyx.Domain.Intelligence;

/// <summary>
/// One piece of provenance behind a recommendation or observation. <see cref="Metrics"/> are allowed only from connected data
/// providers, always with the period they describe; everything else is descriptive text.
/// </summary>
public sealed record EvidenceItem(
    EvidenceSource Source,
    string Description,
    DateTimeOffset? ObservedAtUtc = null,
    DateTimeOffset? PeriodStartUtc = null,
    DateTimeOffset? PeriodEndUtc = null,
    IReadOnlyDictionary<string, double>? Metrics = null)
{
    public const int DescriptionMaxLength = 1_000;
    public const int MaxMetrics = 20;

    public static bool IsDataProvider(EvidenceSource source) => (int)source >= 10;

    /// <summary>Null when valid; otherwise why the evidence is not acceptable.</summary>
    public string? Problem()
    {
        if (!Enum.IsDefined(Source) || string.IsNullOrWhiteSpace(Description) || Description.Length > DescriptionMaxLength)
        {
            return "Evidence needs a known source and a description.";
        }

        if (Metrics is not { Count: > 0 })
        {
            return null;
        }

        if (!IsDataProvider(Source))
        {
            return "Quantitative metrics can only come from a connected data provider.";
        }

        if (PeriodStartUtc is null || PeriodEndUtc is null || PeriodEndUtc < PeriodStartUtc)
        {
            return "Provider metrics must state the period they describe.";
        }

        return Metrics.Count > MaxMetrics || Metrics.Any(metric => string.IsNullOrWhiteSpace(metric.Key) || metric.Key.Length > 64 || !double.IsFinite(metric.Value))
            ? "Invalid metric names or values."
            : null;
    }
}
