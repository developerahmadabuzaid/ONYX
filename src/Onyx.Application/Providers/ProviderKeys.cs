namespace Onyx.Application.Providers;

/// <summary>Stable provider identifiers stored in <c>provider_connections</c>. Never rename existing values.</summary>
public static class ProviderKeys
{
    public const string EmailSmtp = "email.smtp";
    public const string EmailPickupDirectory = "email.pickup-directory";

    // Content intelligence data sources (all NotConfigured until credentials exist and a connection test passes).
    public const string GoogleAnalytics = "analytics.ga4";
    public const string SearchConsole = "search.gsc";
    public const string TextGeneration = "ai.text";
    public const string ImageGeneration = "ai.image";
    public const string OnyxSeoPlatform = "seo.onyx-platform";
    public const string KeywordProvider = "seo.keywords";
    public const string SerpProvider = "seo.serp";
    public const string BehaviorAnalytics = "analytics.behavior";

    // News summaries from Onyx's external AI Agent (Stage 11): NotConfigured until an adapter for the Agent is connected.
    public const string NewsSummaries = "ai.news-summary";

    // Search-engine change notification (IndexNow: Bing and other participants; not Google). Inactive until configured.
    public const string IndexNow = "seo.indexnow";
}
