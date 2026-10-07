using Onyx.Domain.Common;

namespace Onyx.Domain.Analytics;

/// <summary>
/// One daily aggregate row from the connected web-analytics provider (architecture.md §15: synced aggregates, never live calls).
/// Written only by the sync job; no visitor-level identifier, IP address or client id is stored. Dimensions are bounded
/// (<see cref="AnalyticsDimensions"/>) and the row is unique per provider, date, kind and dimension values.
/// </summary>
public sealed class WebAnalyticsDailyMetric : Entity
{
    public const int EventNameMaxLength = 40;
    public const int DeviceMaxLength = 20;

    private WebAnalyticsDailyMetric()
    {
        ProviderKey = string.Empty;
        LandingPage = string.Empty;
        LanguageCode = string.Empty;
        Source = string.Empty;
        Medium = string.Empty;
        Campaign = string.Empty;
        Country = string.Empty;
        DeviceCategory = string.Empty;
        EventName = string.Empty;
        DimensionHash = string.Empty;
    }

    public string ProviderKey { get; private set; }

    public DateOnly Date { get; private set; }

    public WebAnalyticsReportKind Kind { get; private set; }

    /// <summary>Site path (unencoded, no query) for acquisition rows; empty for other kinds.</summary>
    public string LandingPage { get; private set; }

    /// <summary>"ar"/"en" from the landing page's first segment; empty when unknown.</summary>
    public string LanguageCode { get; private set; }

    public string Source { get; private set; }

    public string Medium { get; private set; }

    public string Campaign { get; private set; }

    public string Country { get; private set; }

    public string DeviceCategory { get; private set; }

    public string EventName { get; private set; }

    /// <summary>The CMS page the landing page maps to, when it is a current public path.</summary>
    public Guid? ContentItemId { get; private set; }

    public string DimensionHash { get; private set; }

    /// <summary>Daily active users: only on <see cref="WebAnalyticsReportKind.DailyTotal"/> rows (users never add up across rows).</summary>
    public long? ActiveUsers { get; private set; }

    public long Sessions { get; private set; }

    public long EngagedSessions { get; private set; }

    public long PageViews { get; private set; }

    public long EventCount { get; private set; }

    public DateTimeOffset CollectedAtUtc { get; private set; }

    public static WebAnalyticsDailyMetric DailyTotal(string providerKey, DateOnly date, long activeUsers, long sessions, long engagedSessions, long pageViews, DateTimeOffset now) =>
        Create(providerKey, date, WebAnalyticsReportKind.DailyTotal, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null,
            Math.Max(activeUsers, 0), sessions, engagedSessions, pageViews, 0, now);

    public static WebAnalyticsDailyMetric Acquisition(
        string providerKey, DateOnly date, string landingPage, string languageCode, string source, string medium, string campaign, string country, string device,
        Guid? contentItemId, long sessions, long engagedSessions, long pageViews, DateTimeOffset now) =>
        Create(providerKey, date, WebAnalyticsReportKind.Acquisition, AnalyticsDimensions.Clean(landingPage, AnalyticsDimensions.PathMaxLength), languageCode,
            AnalyticsDimensions.Clean(source, AnalyticsDimensions.ShortMaxLength), AnalyticsDimensions.Clean(medium, AnalyticsDimensions.ShortMaxLength),
            AnalyticsDimensions.Clean(campaign, AnalyticsDimensions.CampaignMaxLength), AnalyticsDimensions.Clean(country, AnalyticsDimensions.ShortMaxLength),
            AnalyticsDimensions.Clean(device, DeviceMaxLength), string.Empty, contentItemId, null, sessions, engagedSessions, pageViews, 0, now);

    public static WebAnalyticsDailyMetric ConversionEvent(string providerKey, DateOnly date, string eventName, long eventCount, DateTimeOffset now) =>
        Create(providerKey, date, WebAnalyticsReportKind.ConversionEvent, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            AnalyticsDimensions.Clean(eventName, EventNameMaxLength), null, null, 0, 0, 0, eventCount, now);

    private static WebAnalyticsDailyMetric Create(
        string providerKey, DateOnly date, WebAnalyticsReportKind kind, string landingPage, string languageCode, string source, string medium, string campaign,
        string country, string device, string eventName, Guid? contentItemId, long? activeUsers, long sessions, long engagedSessions, long pageViews, long eventCount,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return new WebAnalyticsDailyMetric
        {
            ProviderKey = providerKey,
            Date = date,
            Kind = kind,
            LandingPage = landingPage,
            LanguageCode = languageCode is "ar" or "en" ? languageCode : string.Empty,
            Source = source,
            Medium = medium,
            Campaign = campaign,
            Country = country,
            DeviceCategory = device,
            EventName = eventName,
            ContentItemId = contentItemId,
            DimensionHash = AnalyticsDimensions.Hash(kind.ToString(), landingPage, source, medium, campaign, country, device, eventName),
            ActiveUsers = activeUsers,
            Sessions = Math.Max(sessions, 0),
            EngagedSessions = Math.Max(engagedSessions, 0),
            PageViews = Math.Max(pageViews, 0),
            EventCount = Math.Max(eventCount, 0),
            CollectedAtUtc = now,
        };
    }
}
