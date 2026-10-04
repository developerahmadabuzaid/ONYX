using System.Globalization;
using Onyx.Application.Content;
using Onyx.Application.Knowledge;
using Onyx.Application.Localization;
using Onyx.Application.Providers;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Providers;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Reports what the intelligence layer can actually rely on. Every provider-backed source is shown with its real connection
/// status; health is computed only from provider snapshots and is "insufficient data" without them.
/// </summary>
public sealed class IntelligenceStatusService(
    IProviderConnectionRepository connections,
    IContentPerformanceRepository performance,
    ContentAccess access) : IIntelligenceStatusService
{
    public const string ClicksMetric = "clicks";
    public const double DeclineThreshold = 0.25;

    private static readonly (EvidenceSource Source, string ProviderKey, string Purpose)[] Sources =
    [
        (EvidenceSource.WebAnalytics, ProviderKeys.GoogleAnalytics, "Users, sessions, views, acquisition, countries, devices, conversions (Google Analytics 4)."),
        (EvidenceSource.SearchConsole, ProviderKeys.SearchConsole, "Queries, impressions, clicks, CTR and average position (Google Search Console)."),
        (EvidenceSource.OnyxSeoPlatform, ProviderKeys.OnyxSeoPlatform, "Onyx SEO platform analysis (capabilities declared by the platform API once defined)."),
        (EvidenceSource.KeywordProvider, ProviderKeys.KeywordProvider, "Keyword research data from an approved provider."),
        (EvidenceSource.SerpResearch, ProviderKeys.SerpProvider, "Lawful SERP and topic-coverage research from an approved provider."),
        (EvidenceSource.BehaviorAnalytics, ProviderKeys.BehaviorAnalytics, "Heatmaps/session behaviour from a separate, consent-aware provider (not Google Analytics)."),
        (EvidenceSource.RuleEngine, ProviderKeys.TextGeneration, "AI text generation for proposals and drafts (always reviewed by editors)."),
        (EvidenceSource.RuleEngine, ProviderKeys.ImageGeneration, "AI image generation for featured/social images (always reviewed by editors)."),
    ];

    public async Task<IntelligenceCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        new(await StatusesAsync(cancellationToken).ConfigureAwait(false), CompanyKnowledgeSource.GovernanceRules);

    public async Task<Result<ContentIntelligenceResponse>> GetContentProfileAsync(Guid contentItemId, string languageCode, CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return Error.Validation("Content.UnsupportedLanguage", "Unsupported language.");
        }

        var loaded = await access.ForReadAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var snapshots = await performance.ListAsync(contentItemId, languageCode, 24, cancellationToken).ConfigureAwait(false);
        var (health, reasons) = AssessHealth(snapshots);
        return new ContentIntelligenceResponse(
            contentItemId,
            languageCode,
            health.ToString(),
            reasons,
            snapshots.Select(snapshot => new Contracts.Intelligence.PerformanceSnapshotResponse(
                snapshot.Source.ToString(), snapshot.PeriodStartUtc, snapshot.PeriodEndUtc, new Dictionary<string, double>(snapshot.Metrics), snapshot.CollectedAtUtc)).ToList(),
            await StatusesAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Compares the two most recent Search Console periods. Without at least two real periods the answer is
    /// "insufficient data"; a change is described as an observation, not a cause.
    /// </summary>
    public static (ContentHealth Health, IReadOnlyList<string> Reasons) AssessHealth(IReadOnlyList<ContentPerformanceSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var periods = snapshots
            .Where(snapshot => snapshot.Source == EvidenceSource.SearchConsole && snapshot.Metrics.ContainsKey(ClicksMetric))
            .OrderByDescending(snapshot => snapshot.PeriodEndUtc)
            .Take(2)
            .ToList();

        if (periods.Count < 2)
        {
            return (ContentHealth.InsufficientData, ["Fewer than two Search Console periods are available for this page; no health assessment is made."]);
        }

        var latest = periods[0].Metrics[ClicksMetric];
        var previous = periods[1].Metrics[ClicksMetric];
        if (previous <= 0)
        {
            return (ContentHealth.InsufficientData, ["The earlier period has no clicks to compare against."]);
        }

        var change = (latest - previous) / previous;
        var described = string.Create(CultureInfo.InvariantCulture,
            $"Clicks changed by {change:P0} between {periods[1].PeriodStartUtc:yyyy-MM-dd}–{periods[1].PeriodEndUtc:yyyy-MM-dd} and {periods[0].PeriodStartUtc:yyyy-MM-dd}–{periods[0].PeriodEndUtc:yyyy-MM-dd} (Search Console).");
        return change <= -DeclineThreshold ? (ContentHealth.Declining, [described]) : (ContentHealth.Healthy, [described]);
    }

    private async Task<IReadOnlyList<DataSourceStatusResponse>> StatusesAsync(CancellationToken cancellationToken)
    {
        var known = (await connections.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(connection => connection.ProviderKey, StringComparer.Ordinal);
        return Sources
            .Select(source => new DataSourceStatusResponse(
                source.ProviderKey.StartsWith("ai.", StringComparison.Ordinal) ? "Ai" : source.Source.ToString(),
                source.ProviderKey,
                (known.TryGetValue(source.ProviderKey, out var connection) ? connection.Status : ProviderConnectionStatus.NotConfigured).ToString(),
                source.Purpose))
            .ToList();
    }
}
