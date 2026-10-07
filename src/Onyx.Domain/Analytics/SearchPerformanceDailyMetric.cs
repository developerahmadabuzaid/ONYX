using Onyx.Domain.Common;

namespace Onyx.Domain.Analytics;

/// <summary>
/// One daily aggregate row from the connected search-performance provider. Clicks and impressions add up across rows of the
/// same kind; CTR is always recomputed as clicks / impressions, and average position is combined weighted by impressions.
/// Written only by the sync job. Unique per provider, date, kind and dimension values.
/// </summary>
public sealed class SearchPerformanceDailyMetric : Entity
{
    public const int DeviceMaxLength = 20;

    private SearchPerformanceDailyMetric()
    {
        ProviderKey = string.Empty;
        Page = string.Empty;
        LanguageCode = string.Empty;
        Query = string.Empty;
        Country = string.Empty;
        Device = string.Empty;
        DimensionHash = string.Empty;
    }

    public string ProviderKey { get; private set; }

    public DateOnly Date { get; private set; }

    public SearchPerformanceReportKind Kind { get; private set; }

    /// <summary>Site path (unencoded, no query) for page rows; empty otherwise.</summary>
    public string Page { get; private set; }

    /// <summary>"ar"/"en" from the page path; empty when unknown or not a page row. Arabic and English pages are never merged.</summary>
    public string LanguageCode { get; private set; }

    public string Query { get; private set; }

    public string Country { get; private set; }

    public string Device { get; private set; }

    public Guid? ContentItemId { get; private set; }

    public string DimensionHash { get; private set; }

    public long Clicks { get; private set; }

    public long Impressions { get; private set; }

    /// <summary>Average position as reported by the provider for this row.</summary>
    public double AveragePosition { get; private set; }

    public DateTimeOffset CollectedAtUtc { get; private set; }

    public static SearchPerformanceDailyMetric Create(
        string providerKey, DateOnly date, SearchPerformanceReportKind kind, string? page, string languageCode, string? query, string? country, string? device,
        Guid? contentItemId, long clicks, long impressions, double averagePosition, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        var cleanPage = kind == SearchPerformanceReportKind.Page ? AnalyticsDimensions.Clean(page, AnalyticsDimensions.PathMaxLength) : string.Empty;
        var cleanQuery = kind == SearchPerformanceReportKind.Query ? AnalyticsDimensions.Clean(query, AnalyticsDimensions.QueryMaxLength) : string.Empty;
        var cleanCountry = kind == SearchPerformanceReportKind.CountryDevice ? AnalyticsDimensions.Clean(country, AnalyticsDimensions.ShortMaxLength) : string.Empty;
        var cleanDevice = kind == SearchPerformanceReportKind.CountryDevice ? AnalyticsDimensions.Clean(device, DeviceMaxLength) : string.Empty;
        return new SearchPerformanceDailyMetric
        {
            ProviderKey = providerKey,
            Date = date,
            Kind = kind,
            Page = cleanPage,
            LanguageCode = kind == SearchPerformanceReportKind.Page && languageCode is "ar" or "en" ? languageCode : string.Empty,
            Query = cleanQuery,
            Country = cleanCountry,
            Device = cleanDevice,
            ContentItemId = contentItemId,
            DimensionHash = AnalyticsDimensions.Hash(kind.ToString(), cleanPage, cleanQuery, cleanCountry, cleanDevice),
            Clicks = Math.Max(clicks, 0),
            Impressions = Math.Max(impressions, 0),
            AveragePosition = double.IsFinite(averagePosition) && averagePosition > 0 ? averagePosition : 0,
            CollectedAtUtc = now,
        };
    }
}
