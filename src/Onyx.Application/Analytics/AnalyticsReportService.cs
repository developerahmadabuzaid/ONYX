using Onyx.Application.Content;
using Onyx.Application.Providers;
using Onyx.Contracts.Analytics;
using Onyx.Domain.Analytics;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;
using Onyx.Domain.Providers;

namespace Onyx.Application.Analytics;

/// <summary>
/// Admin analytics reports (architecture.md §15) from local data only: provider aggregates (GA4, Search Console) and Onyx's
/// first-party funnel (Stage 8 attribution, lead stages). The two are separate datasets: GA4 rows carry no visitor identifier,
/// so nothing is joined per visitor or per lead. Where a report puts them side by side, it is an aggregate comparison by a
/// shared key (landing path, campaign, event) and says so. Unavailable data is reported as a state, never as zero.
/// </summary>
public sealed class AnalyticsReportService(
    IAnalyticsMetricRepository metrics,
    IAnalyticsSyncStateRepository states,
    IAnalyticsFunnelRepository funnel,
    IProviderConnectionRepository connections,
    IContentRepository content,
    IContentPathRepository paths,
    AnalyticsSettings settings,
    TimeProvider timeProvider)
{
    public const string FirstParty = "onyx";
    public const int MaxRows = 200;
    public const int MaxFacts = 50_000;
    public const int MaxPeriodDays = 366;
    public const int DefaultPeriodDays = 28;

    public const string NoteSeparateDatasets = "Analytics.Note.SeparateDatasets";
    public const string NoteConsent = "Analytics.Note.ConsentLimited";
    public const string NoteSearchDelay = "Analytics.Note.SearchDelay";
    public const string NoteSignals = "Analytics.Note.Signals";
    public const string NoteCurrentStage = "Analytics.Note.CurrentStage";
    public const string NoteUsersNotAdditive = "Analytics.Note.UsersNotAdditive";

    private static readonly string Ga4 = ProviderKeys.GoogleAnalytics;
    private static readonly string Gsc = ProviderKeys.SearchConsole;
    private static readonly string[] Traffic = ["sessions", "engagedSessions", "pageViews"];
    private static readonly string[] Search = ["clicks", "impressions", "ctr", "position"];
    private static readonly string[] StageColumns = ["leads", "New", "Contacted", "Qualified", "Proposal", "Won", "Lost"];

    public Task<Result<AnalyticsReportResponse>> OverviewAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("overview", query, Ga4, async (from, to, token) =>
        {
            var days = await metrics.WebTotalsAsync(Ga4, from, to, token).ConfigureAwait(false);
            var leads = await funnel.ListLeadFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var inquiries = await funnel.ListInquiryFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var bookings = await funnel.CountBookingsAsync(Start(from), End(to), token).ConfigureAwait(false);
            var daily = days.Select(day => Row(day.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), null,
                day.ActiveUsers, day.Sessions, day.EngagedSessions, day.PageViews)).ToList();
            if (days.Count > 0)
            {
                // Users are not additive across days, so the total row has no user count.
                daily.Add(new AnalyticsRowResponse("total", null, [null, days.Sum(day => day.Sessions), days.Sum(day => day.EngagedSessions), days.Sum(day => day.PageViews)]));
            }

            return (new List<AnalyticsSectionResponse>
            {
                new("webDaily", Ga4, ["activeUsers", .. Traffic], daily),
                new("firstParty", FirstParty, ["count"],
                [
                    Row("inquiries", null, inquiries.Count),
                    Row("newLeads", null, leads.Count),
                    Row("bookings", null, bookings),
                    Row("won", null, leads.Count(lead => lead.Stage == LeadStage.Won)),
                    Row("lost", null, leads.Count(lead => lead.Stage == LeadStage.Lost)),
                ]),
            }, new List<string> { NoteUsersNotAdditive, NoteSeparateDatasets, NoteCurrentStage });
        }, cancellationToken);

    public Task<Result<AnalyticsReportResponse>> TrafficAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("traffic", query, Ga4, async (from, to, token) =>
            (new List<AnalyticsSectionResponse>
            {
                await WebSectionAsync("sourceMedium", WebDimension.SourceMedium, from, to, token).ConfigureAwait(false),
                await WebSectionAsync("country", WebDimension.Country, from, to, token).ConfigureAwait(false),
                await WebSectionAsync("device", WebDimension.Device, from, to, token).ConfigureAwait(false),
            }, new List<string> { NoteConsent }), cancellationToken);

    /// <summary>GA4 landing pages, Onyx inquiries by landing path, and pages with traffic but no first-party inquiry.</summary>
    public Task<Result<AnalyticsReportResponse>> LandingPagesAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("landingPages", query, Ga4, async (from, to, token) =>
        {
            var traffic = await metrics.WebBreakdownAsync(Ga4, from, to, WebDimension.LandingPage, MaxRows, token).ConfigureAwait(false);
            var inquiries = await funnel.ListInquiryFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var byPath = inquiries
                .GroupBy(inquiry => AnalyticsPaths.Normalize(Touch(inquiry.Attribution, query.Touch)?.LandingPath).Path)
                .ToDictionary(group => group.Key.Length == 0 ? AnalyticsDimensions.NotSet : group.Key, group => (long)group.Count(), StringComparer.Ordinal);
            var quiet = traffic
                .Where(row => row.Key is not (AnalyticsDimensions.Other or AnalyticsDimensions.NotSet) && row.Sessions >= AnalyticsSignalRules.MinTrafficWithoutInquiries
                    && !byPath.ContainsKey(row.Key))
                .Take(AnalyticsSignalRules.MaxRowsPerSignal)
                .Select(row => Row(row.Key, Language(row.LanguageCode), row.Sessions))
                .ToList();
            return (new List<AnalyticsSectionResponse>
            {
                new("ga4LandingPages", Ga4, Traffic, traffic.Select(row => Row(row.Key, Language(row.LanguageCode), row.Sessions, row.EngagedSessions, row.PageViews)).ToList()),
                new("onyxInquiriesByLandingPage", FirstParty, ["inquiries"],
                    byPath.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Take(MaxRows)
                        .Select(pair => Row(pair.Key, Language(AnalyticsPaths.Normalize(pair.Key).LanguageCode), pair.Value)).ToList()),
                new("signalTrafficWithoutInquiries", Ga4, ["sessions"], quiet),
            }, new List<string> { NoteSeparateDatasets, NoteConsent, NoteSignals });
        }, cancellationToken);

    /// <summary>GA4 campaigns and Onyx leads by UTM campaign, side by side by campaign name (an aggregate comparison only).</summary>
    public Task<Result<AnalyticsReportResponse>> CampaignsAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("campaigns", query, Ga4, async (from, to, token) =>
        {
            var traffic = await metrics.WebBreakdownAsync(Ga4, from, to, WebDimension.Campaign, MaxRows, token).ConfigureAwait(false);
            var leads = await funnel.ListLeadFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var leadsByCampaign = leads
                .GroupBy(lead => Key(Touch(lead.Attribution, query.Touch)?.UtmCampaign), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var comparison = traffic
                .Where(row => row.Key is not (AnalyticsDimensions.Other or AnalyticsDimensions.NotSet or "(direct)" or "(organic)" or "(referral)") && leadsByCampaign.ContainsKey(row.Key))
                .Select(row => Row(row.Key, null, row.Sessions, leadsByCampaign[row.Key].Count, leadsByCampaign[row.Key].Count(lead => lead.Stage == LeadStage.Won)))
                .ToList();
            return (new List<AnalyticsSectionResponse>
            {
                new("ga4Campaigns", Ga4, Traffic, traffic.Select(row => Row(row.Key, null, row.Sessions, row.EngagedSessions, row.PageViews)).ToList()),
                Funnel("onyxLeadsByCampaign", leadsByCampaign),
                new("campaignComparison", Ga4 + "+" + FirstParty, ["ga4Sessions", "onyxLeads", "onyxWon"], comparison),
            }, new List<string> { NoteSeparateDatasets, NoteConsent, NoteCurrentStage });
        }, cancellationToken);

    /// <summary>GA4 conversion events next to Onyx's own counts for the same workflows and period.</summary>
    public Task<Result<AnalyticsReportResponse>> ConversionsAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("conversions", query, Ga4, async (from, to, token) =>
        {
            var events = (await metrics.WebEventsAsync(Ga4, from, to, token).ConfigureAwait(false)).ToDictionary(row => row.EventName, row => row.EventCount, StringComparer.Ordinal);
            var inquiries = await funnel.ListInquiryFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var bookings = await funnel.CountBookingsAsync(Start(from), End(to), token).ConfigureAwait(false);
            var formInquiries = inquiries.Count(inquiry => inquiry.Type != InquiryType.Consultation);
            return (new List<AnalyticsSectionResponse>
            {
                new("conversionComparison", Ga4 + "+" + FirstParty, ["ga4Events", "onyxRecords"],
                [
                    new(ConversionEvents.InquirySubmitted, null, [events.TryGetValue(ConversionEvents.InquirySubmitted, out var submitted) ? submitted : null, formInquiries]),
                    new(ConversionEvents.ConsultationBookingConfirmed, null,
                        [events.TryGetValue(ConversionEvents.ConsultationBookingConfirmed, out var booked) ? booked : null, bookings]),
                ]),
                new("onyxInquiriesByType", FirstParty, ["inquiries"],
                    inquiries.GroupBy(inquiry => inquiry.Type).OrderBy(group => group.Key).Select(group => Row(group.Key.ToString(), null, group.Count())).ToList()),
            }, new List<string> { NoteConsent, NoteSeparateDatasets });
        }, cancellationToken);

    /// <summary>GA4 sessions per CMS page and language (landing pages mapped to their current public path).</summary>
    public Task<Result<AnalyticsReportResponse>> ContentAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("content", query, Ga4, async (from, to, token) =>
        {
            var rows = await metrics.WebContentAsync(Ga4, from, to, MaxRows, token).ConfigureAwait(false);
            var titles = await TitlesAsync(rows.Select(row => row.ContentItemId).ToList(), token).ConfigureAwait(false);
            return (new List<AnalyticsSectionResponse>
            {
                new("ga4Content", Ga4, Traffic, rows.Select(row => Row(Title(titles, row.ContentItemId, row.LanguageCode), row.LanguageCode, row.Sessions, row.EngagedSessions, row.PageViews)).ToList()),
            }, new List<string> { NoteConsent });
        }, cancellationToken);

    /// <summary>The first-party lead funnel grouped by one attribution dimension (Stage 8 attribution, current lead stage).</summary>
    public Task<Result<AnalyticsReportResponse>> FunnelAsync(AnalyticsReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var groupBy = (query.GroupBy ?? "source").Trim().ToLowerInvariant();
        if (groupBy is not ("source" or "medium" or "campaign" or "landingpage" or "service" or "market" or "language" or "inquirytype"))
        {
            return Task.FromResult<Result<AnalyticsReportResponse>>(Error.Validation("Analytics.InvalidGroup", "Unknown grouping."));
        }

        return BuildAsync("funnel", query, null, async (from, to, token) =>
        {
            var leads = await funnel.ListLeadFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var serviceTitles = groupBy == "service"
                ? await TitlesAsync(leads.Where(lead => lead.ServiceId is not null).Select(lead => lead.ServiceId!.Value).Distinct().ToList(), token).ConfigureAwait(false)
                : new Dictionary<Guid, Domain.Content.ContentItem>();
            var groups = leads.GroupBy(lead => groupBy switch
            {
                "medium" => Key(Touch(lead.Attribution, query.Touch)?.UtmMedium),
                "campaign" => Key(Touch(lead.Attribution, query.Touch)?.UtmCampaign),
                "landingpage" => Key(AnalyticsPaths.Normalize(Touch(lead.Attribution, query.Touch)?.LandingPath).Path),
                "service" => lead.ServiceId is { } service ? Title(serviceTitles, service, lead.LanguageCode) : AnalyticsDimensions.NotSet,
                "market" => Key(lead.Attribution.Market),
                "language" => Key(lead.LanguageCode),
                "inquirytype" => lead.FirstInquiryType.ToString(),
                _ => Key(Touch(lead.Attribution, query.Touch)?.UtmSource),
            }, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
            var all = new Dictionary<string, List<LeadFunnelFact>>(StringComparer.Ordinal) { ["total"] = leads.ToList() };
            return (new List<AnalyticsSectionResponse> { Funnel("funnelTotal", all), Funnel("funnelBy:" + groupBy, groups) },
                new List<string> { NoteCurrentStage, "Analytics.Note.Touch." + (IsFirstTouch(query.Touch) ? "first" : "last") });
        }, cancellationToken);
    }

    public Task<Result<AnalyticsReportResponse>> SearchOverviewAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("searchOverview", query, Gsc, async (from, to, token) =>
        {
            var days = await metrics.SearchTotalsAsync(Gsc, from, to, token).ConfigureAwait(false);
            var rows = days.Select(day => Row(day.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), null, day.Clicks, day.Impressions,
                day.Impressions == 0 ? 0 : Math.Round((double)day.Clicks / day.Impressions, 4), Math.Round(day.AveragePosition, 2))).ToList();
            if (days.Count > 0)
            {
                var clicks = days.Sum(day => day.Clicks);
                var impressions = days.Sum(day => day.Impressions);
                var position = impressions == 0 ? 0 : days.Sum(day => day.AveragePosition * day.Impressions) / impressions;
                rows.Add(new AnalyticsRowResponse("total", null, [clicks, impressions, impressions == 0 ? 0 : Math.Round((double)clicks / impressions, 4), Math.Round(position, 2)]));
            }

            return (new List<AnalyticsSectionResponse>
            {
                new("searchDaily", Gsc, Search, rows),
                await SearchSectionAsync("searchCountry", SearchDimension.Country, from, to, token).ConfigureAwait(false),
                await SearchSectionAsync("searchDevice", SearchDimension.Device, from, to, token).ConfigureAwait(false),
            }, new List<string> { NoteSearchDelay });
        }, cancellationToken);

    public Task<Result<AnalyticsReportResponse>> SearchQueriesAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("searchQueries", query, Gsc, async (from, to, token) =>
            (new List<AnalyticsSectionResponse> { await SearchSectionAsync("searchQueries", SearchDimension.Query, from, to, token).ConfigureAwait(false) },
                new List<string> { NoteSearchDelay }), cancellationToken);

    /// <summary>Search pages, each in its own language (Arabic and English pages are separate rows), with the CMS page title when mapped.</summary>
    public Task<Result<AnalyticsReportResponse>> SearchPagesAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("searchPages", query, Gsc, async (from, to, token) =>
        {
            var rows = await metrics.SearchBreakdownAsync(Gsc, from, to, SearchDimension.Page, MaxRows, token).ConfigureAwait(false);
            var titles = await TitlesAsync(rows.Where(row => row.ContentItemId is not null).Select(row => row.ContentItemId!.Value).ToList(), token).ConfigureAwait(false);
            return (new List<AnalyticsSectionResponse>
            {
                new("searchPages", Gsc, ["clicks", "impressions", "ctr", "position"], rows.Select(row => SearchRow(
                    row.ContentItemId is { } id ? $"{Title(titles, id, row.LanguageCode)} — {row.Key}" : row.Key, row)).ToList()),
            }, new List<string> { NoteSearchDelay });
        }, cancellationToken);

    /// <summary>
    /// Deterministic search signals for the period against the previous period of the same length: high impressions with low
    /// CTR, rising or falling clicks, queries near the first page, visibility without first-party inquiries, and live pages with
    /// no recorded impressions. Observations to review, not recommendations, and never applied automatically.
    /// </summary>
    public Task<Result<AnalyticsReportResponse>> SearchSignalsAsync(AnalyticsReportQuery query, CancellationToken cancellationToken) =>
        BuildAsync("searchSignals", query, Gsc, async (from, to, token) =>
        {
            var length = to.DayNumber - from.DayNumber + 1;
            var pages = await metrics.SearchBreakdownAsync(Gsc, from, to, SearchDimension.Page, MaxRows * 5, token).ConfigureAwait(false);
            var previous = (await metrics.SearchBreakdownAsync(Gsc, from.AddDays(-length), from.AddDays(-1), SearchDimension.Page, MaxRows * 5, token).ConfigureAwait(false))
                .ToDictionary(row => row.Key, StringComparer.Ordinal);
            var queries = await metrics.SearchBreakdownAsync(Gsc, from, to, SearchDimension.Query, MaxRows * 5, token).ConfigureAwait(false);
            var inquiries = await funnel.ListInquiryFactsAsync(Start(from), End(to), MaxFacts, token).ConfigureAwait(false);
            var landed = inquiries.Select(inquiry => AnalyticsPaths.Normalize(inquiry.Attribution.LastTouch?.LandingPath).Path)
                .Concat(inquiries.Select(inquiry => AnalyticsPaths.Normalize(inquiry.Attribution.FirstTouch?.LandingPath).Path))
                .Where(path => path.Length > 0).ToHashSet(StringComparer.Ordinal);
            var real = pages.Where(row => row.Key is not (AnalyticsDimensions.Other or AnalyticsDimensions.NotSet)).ToList();

            var lowCtr = real.Concat(queries.Where(row => row.Key is not (AnalyticsDimensions.Other or AnalyticsDimensions.NotSet)))
                .Where(row => row.Impressions >= AnalyticsSignalRules.MinImpressionsForCtr && row.Ctr < AnalyticsSignalRules.LowCtr)
                .OrderByDescending(row => row.Impressions).Take(AnalyticsSignalRules.MaxRowsPerSignal);
            var trends = real
                .Where(row => previous.TryGetValue(row.Key, out var before) && before.Clicks >= AnalyticsSignalRules.MinClicksForTrend)
                .Select(row => (Row: row, Before: previous[row.Key], Change: (double)(row.Clicks - previous[row.Key].Clicks) / previous[row.Key].Clicks))
                .Where(item => Math.Abs(item.Change) >= AnalyticsSignalRules.TrendChange)
                .OrderBy(item => item.Change).ToList();
            var opportunities = queries
                .Where(row => row.Key is not (AnalyticsDimensions.Other or AnalyticsDimensions.NotSet) && row.Impressions >= AnalyticsSignalRules.OpportunityMinImpressions
                    && row.AveragePosition >= AnalyticsSignalRules.OpportunityMinPosition && row.AveragePosition <= AnalyticsSignalRules.OpportunityMaxPosition)
                .OrderByDescending(row => row.Impressions).Take(AnalyticsSignalRules.MaxRowsPerSignal);
            var visibleWithoutInquiries = real
                .Where(row => row.Clicks >= AnalyticsSignalRules.MinTrafficWithoutInquiries / 10 && row.Impressions >= AnalyticsSignalRules.MinImpressionsForCtr && !landed.Contains(row.Key))
                .OrderByDescending(row => row.Clicks).Take(AnalyticsSignalRules.MaxRowsPerSignal);

            var sections = new List<AnalyticsSectionResponse>
            {
                new("signalHighImpressionsLowCtr", Gsc, Search, lowCtr.Select(row => SearchRow(row.Key, row)).ToList()),
                new("signalClicksDeclining", Gsc, ["clicks", "previousClicks", "change"],
                    trends.Where(item => item.Change < 0).Take(AnalyticsSignalRules.MaxRowsPerSignal)
                        .Select(item => Row(item.Row.Key, Language(item.Row.LanguageCode), item.Row.Clicks, item.Before.Clicks, Math.Round(item.Change, 3))).ToList()),
                new("signalClicksImproving", Gsc, ["clicks", "previousClicks", "change"],
                    trends.Where(item => item.Change > 0).OrderByDescending(item => item.Change).Take(AnalyticsSignalRules.MaxRowsPerSignal)
                        .Select(item => Row(item.Row.Key, Language(item.Row.LanguageCode), item.Row.Clicks, item.Before.Clicks, Math.Round(item.Change, 3))).ToList()),
                new("signalQueryOpportunities", Gsc, Search, opportunities.Select(row => SearchRow(row.Key, row)).ToList()),
                new("signalVisibleWithoutInquiries", Gsc + "+" + FirstParty, ["clicks", "impressions"],
                    visibleWithoutInquiries.Select(row => Row(row.Key, Language(row.LanguageCode), row.Clicks, row.Impressions)).ToList()),
            };

            // "Little measurable visibility": live CMS pages with no impressions at all, only when the provider has data for the period.
            if (pages.Count > 0)
            {
                var seen = pages.Where(row => row.ContentItemId is not null).Select(row => (row.ContentItemId!.Value, row.LanguageCode)).ToHashSet();
                var live = await content.ListLiveAsync(token).ConfigureAwait(false);
                var current = await paths.GetCurrentPathsAsync(live.Where(item => item.HasPage).Select(item => item.Id).ToList(), token).ConfigureAwait(false);
                sections.Add(new AnalyticsSectionResponse("signalNoVisibility", Gsc, [],
                    current.Where(pair => !seen.Contains(pair.Key))
                        .Select(pair => AnalyticsPaths.Normalize($"/{pair.Key.LanguageCode}/{pair.Value}"))
                        .OrderBy(path => path.Path, StringComparer.Ordinal).Take(AnalyticsSignalRules.MaxRowsPerSignal)
                        .Select(path => new AnalyticsRowResponse(path.Path, Language(path.LanguageCode), [])).ToList()));
            }

            return (sections, new List<string> { NoteSignals, NoteSearchDelay, NoteSeparateDatasets });
        }, cancellationToken);

    /// <summary>The state of one provider's data (never its credentials).</summary>
    public async Task<AnalyticsDataStatusResponse> StatusAsync(string providerKey, bool hasDataInPeriod, CancellationToken cancellationToken)
    {
        var connection = await connections.GetByKeyAsync(providerKey, cancellationToken).ConfigureAwait(false);
        var state = await states.GetAsync(providerKey, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var status = connection?.Status ?? ProviderConnectionStatus.NotConfigured;
        var name = status switch
        {
            ProviderConnectionStatus.Disabled => "Disabled",
            ProviderConnectionStatus.Error => "Failed",
            ProviderConnectionStatus.NotConfigured => "NotConfigured",
            _ => hasDataInPeriod ? "Ok" : "NoData",
        };
        var stale = state?.LastSuccessAtUtc is { } success && now - success > settings.StaleAfter && status != ProviderConnectionStatus.Disabled;
        return new AnalyticsDataStatusResponse(
            providerKey, name, stale, state?.IsSyncing ?? false, state?.LastSuccessAtUtc, state?.LastFailureAtUtc, state?.LastFailureCode,
            state?.DataFromDate, state?.DataThroughDate, state?.LastRunTruncated ?? false);
    }

    /// <summary>The period to report: the requested one, or the latest 28 days with data; bounded to 366 days and not in the future.</summary>
    public async Task<Result<(DateOnly From, DateOnly To)>> PeriodAsync(AnalyticsReportQuery query, string? providerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var latest = providerKey is null ? today : (await states.GetAsync(providerKey, cancellationToken).ConfigureAwait(false))?.DataThroughDate ?? today.AddDays(-1);
        var to = query.To ?? (query.From is { } start ? start.AddDays(DefaultPeriodDays - 1) : latest);
        var from = query.From ?? to.AddDays(1 - DefaultPeriodDays);
        if (to > today)
        {
            to = today;
        }

        if (from > to || to.DayNumber - from.DayNumber + 1 > MaxPeriodDays || from < today.AddYears(-3))
        {
            return Error.Validation("Analytics.InvalidPeriod", $"Choose a period of 1–{MaxPeriodDays} days, ending today or earlier, within the last 3 years.");
        }

        return (from, to);
    }

    private async Task<Result<AnalyticsReportResponse>> BuildAsync(
        string report, AnalyticsReportQuery query, string? providerKey,
        Func<DateOnly, DateOnly, CancellationToken, Task<(List<AnalyticsSectionResponse> Sections, List<string> Notes)>> build, CancellationToken cancellationToken)
    {
        var period = await PeriodAsync(query, providerKey, cancellationToken).ConfigureAwait(false);
        if (period.IsFailure)
        {
            return period.Error!;
        }

        var (from, to) = period.Value;
        var (sections, notes) = await build(from, to, cancellationToken).ConfigureAwait(false);
        var sources = new List<AnalyticsDataStatusResponse>();
        foreach (var provider in sections.SelectMany(section => section.Source.Split('+')).Distinct(StringComparer.Ordinal))
        {
            var hasData = sections.Any(section => section.Source.Split('+')[0] == provider && section.Rows.Count > 0);
            sources.Add(provider == FirstParty
                ? new AnalyticsDataStatusResponse(FirstParty, hasData ? "Ok" : "NoData", false, false, null, null, null, null, null, false)
                : await StatusAsync(provider, hasData, cancellationToken).ConfigureAwait(false));
        }

        return new AnalyticsReportResponse(report, from, to, sources, sections, notes);
    }

    private async Task<AnalyticsSectionResponse> WebSectionAsync(string key, WebDimension dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        new(key, Ga4, Traffic, (await metrics.WebBreakdownAsync(Ga4, from, to, dimension, MaxRows, cancellationToken).ConfigureAwait(false))
            .Select(row => Row(row.Key, Language(row.LanguageCode), row.Sessions, row.EngagedSessions, row.PageViews)).ToList());

    private async Task<AnalyticsSectionResponse> SearchSectionAsync(string key, SearchDimension dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        new(key, Gsc, Search, (await metrics.SearchBreakdownAsync(Gsc, from, to, dimension, MaxRows, cancellationToken).ConfigureAwait(false))
            .Select(row => SearchRow(row.Key, row)).ToList());

    private static AnalyticsSectionResponse Funnel(string key, IReadOnlyDictionary<string, List<LeadFunnelFact>> groups) =>
        new(key, FirstParty, StageColumns, groups
            .OrderByDescending(pair => pair.Value.Count).ThenBy(pair => pair.Key, StringComparer.Ordinal).Take(MaxRows)
            .Select(pair => new AnalyticsRowResponse(pair.Key, null,
                [pair.Value.Count, .. Enum.GetValues<LeadStage>().Select(stage => (double?)pair.Value.Count(lead => lead.Stage == stage))]))
            .ToList());

    private static AnalyticsRowResponse SearchRow(string label, SearchBreakdownRow row) =>
        Row(label, Language(row.LanguageCode), row.Clicks, row.Impressions, Math.Round(row.Ctr, 4), Math.Round(row.AveragePosition, 2));

    private static AnalyticsRowResponse Row(string label, string? language, params double[] values) => new(label, language, values.Select(value => (double?)value).ToList());

    private static string? Language(string? code) => code is "ar" or "en" ? code : null;

    private static string Key(string? value) => string.IsNullOrWhiteSpace(value) ? AnalyticsDimensions.NotSet : value.Trim();

    private static bool IsFirstTouch(string? touch) => string.Equals(touch, "first", StringComparison.OrdinalIgnoreCase);

    private static TouchPoint? Touch(Attribution attribution, string? touch) => IsFirstTouch(touch) ? attribution.FirstTouch : attribution.LastTouch;

    private static DateTimeOffset Start(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateTimeOffset End(DateOnly date) => Start(date).AddDays(1);

    private async Task<IReadOnlyDictionary<Guid, Domain.Content.ContentItem>> TitlesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? new Dictionary<Guid, Domain.Content.ContentItem>() : await content.GetLookupAsync(ids.Distinct().ToList(), cancellationToken).ConfigureAwait(false);

    private static string Title(IReadOnlyDictionary<Guid, Domain.Content.ContentItem> titles, Guid id, string languageCode) =>
        titles.TryGetValue(id, out var item)
            ? item.Translations.FirstOrDefault(translation => translation.LanguageCode == languageCode)?.Title
              ?? item.Translations.FirstOrDefault()?.Title ?? id.ToString()
            : id.ToString();
}
