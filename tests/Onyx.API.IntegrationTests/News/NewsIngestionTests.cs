using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.News;
using Onyx.Contracts.Content;
using Onyx.Contracts.News;
using Onyx.Contracts.Search;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.News;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;

namespace Onyx.API.IntegrationTests.News;

/// <summary>The Worker pipeline end to end on PostgreSQL, with the feed (network) and the external summary service controlled.</summary>
public sealed class NewsIngestionTests(PostgresFixture fixture) : NewsApiTestBase(fixture)
{
    private const string FeedA = "https://publisher-sample.com/feed-a.xml";
    private const string FeedB = "https://publisher-sample.com/feed-b.xml";

    [Fact]
    public async Task Without_a_summary_service_items_are_held_never_public_and_ingestion_is_idempotent()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        await CreateAllowedSourceAsync(owner, SourceRequest("Sample Publisher", FeedA, category.Id, autoPublish: true));
        var published = Time.GetUtcNow().AddHours(-3);
        Feeds.Serve(
            FeedA,
            Entry("story-1", "Sample story one", published),
            new FeedEntry(new Uri("https://Publisher-Sample.com/news/story-1?utm_source=rss#top"), "Sample story one again", null, [], null),   // same article
            Entry("story-2", "Sample story two"),                                                                                            // no date
            new FeedEntry(new Uri("javascript:alert(1)"), "Unsafe link", null, [], null),
            new FeedEntry(new Uri("https://10.0.0.5/internal"), "Internal link", null, [], null),
            new FeedEntry(null, "No link", null, [], null));

        var first = await IngestAsync();

        var items = await QueryAsync(db => db.NewsItems.Include(item => item.Translations).OrderBy(item => item.OriginalUrl));
        Assert.Equal(["https://publisher-sample.com/news/story-1", "https://publisher-sample.com/news/story-2"], items.Select(item => item.OriginalUrl));
        Assert.All(items, item =>
        {
            Assert.Equal(NewsItemStatus.Held, item.Status);
            Assert.Equal(NewsHoldReason.SummaryUnavailable, item.HoldReason);
            Assert.Empty(item.Translations);
            Assert.Equal("Sample Publisher", item.Publisher);
        });
        Assert.Equal(published, items[0].SourcePublishedAtUtc);
        Assert.Null(items[1].SourcePublishedAtUtc);
        Assert.Contains("items new 2, auto-published 0, held 2", first.Message, StringComparison.Ordinal);
        Assert.Equal(ProviderConnectionStatus.NotConfigured, (await QueryAsync(db => db.ProviderConnections.Where(c => c.ProviderKey == "ai.news-summary"))).Single().Status);

        // Nothing held is public: no hub entries, no item page, no search hit, no sitemap entry.
        var hub = await ReadAsync<PublicNewsListResponse>(await Client.GetAsync("/api/v1/news?language=en", Token));
        Assert.Empty(hub.Items);
        Assert.Empty(await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/news/sitemap?language=en", Token)));
        Assert.Empty(await ReadAsync<List<PublicNewsItemSummaryResponse>>(await Client.GetAsync("/api/v1/news/latest?language=en", Token)));
        Assert.Empty((await ReadAsync<SiteSearchResponse>(await Client.GetAsync("/api/v1/search?language=en&q=sample%20story", Token))).Groups);
        Assert.Empty(await QueryAsync(db => db.SearchDocuments));

        // A repeated run (also after the source's interval) creates nothing new.
        Time.Advance(TimeSpan.FromMinutes(61));
        var second = await IngestAsync();
        Assert.Contains("items new 0", second.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await QueryAsync(db => db.NewsItems)).Count);

        // The feed's description is processed transiently and never stored anywhere.
        await using var db = await DbAsync();
        var stored = await db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM news_items i LEFT JOIN news_item_translations t ON t.news_item_id = i.id WHERE i.original_title LIKE {'%' + ExcerptMarker + '%'} OR t.summary LIKE {'%' + ExcerptMarker + '%'} OR t.title LIKE {'%' + ExcerptMarker + '%'}")
            .SingleAsync(Token);
        Assert.Equal(0, stored);
        var auditChanges = await QueryAsync(db => db.AuditLogs.Where(log => log.Changes != null).Select(log => log.Changes!));
        Assert.DoesNotContain(auditChanges, changes => changes.Contains(ExcerptMarker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generated_summaries_publish_automatically_only_from_auto_publishing_allowlisted_sources()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        await CreateAllowedSourceAsync(owner, SourceRequest("Auto Publisher", FeedA, category.Id, autoPublish: true));
        await CreateAllowedSourceAsync(owner, SourceRequest("Reviewed Publisher", FeedB, category.Id, autoPublish: false));
        Feeds.Serve(FeedA, Entry("auto-1", "Automatic sample story", Time.GetUtcNow().AddHours(-1)));
        Feeds.Serve(FeedB, Entry("held-1", "Reviewed sample story"));
        Summaries = FakeSummaryService.Writing();

        await IngestAsync();

        var items = await QueryAsync(db => db.NewsItems.Include(item => item.Translations));
        var automatic = items.Single(item => item.Publisher == "Auto Publisher");
        var reviewed = items.Single(item => item.Publisher == "Reviewed Publisher");
        Assert.Equal(NewsItemStatus.Published, automatic.Status);
        Assert.True(automatic.AutoPublished);
        Assert.False(automatic.Indexable);
        Assert.Equal(NewsSummaryOrigin.Generated, automatic.SummaryOrigin);
        Assert.Equal(NewsItemStatus.Held, reviewed.Status);
        Assert.Equal(NewsHoldReason.AutoPublishOff, reviewed.HoldReason);
        Assert.Equal(2, reviewed.Translations.Count);
        Assert.Contains(AuditAction.NewsItemAutoPublished, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == automatic.Id.ToString()).Select(log => log.Action)));
        Assert.Equal(ProviderConnectionStatus.Active, (await QueryAsync(db => db.ProviderConnections.Where(c => c.ProviderKey == "ai.news-summary"))).Single().Status);

        // The request carries only the structured task: source facts as data, the languages and the limits.
        var request = Summaries.Requests.First(candidate => candidate.SourceName == "Auto Publisher");
        Assert.Equal(["ar", "en"], request.TargetLanguages);
        Assert.Equal("https://publisher-sample.com/news/auto-1", request.OriginalUrl);
        Assert.Equal(NewsItemTranslation.SummaryMaxLength, request.MaxSummaryCharacters);

        // Public with attribution, but noindex by default: not in the sitemap item list and not in site search.
        var hub = await ReadAsync<PublicNewsListResponse>(await Client.GetAsync("/api/v1/news?language=en", Token));
        var listed = Assert.Single(hub.Items);
        Assert.Equal("Auto Publisher", listed.Publisher);
        var slug = listed.Path.Split('/')[^1];
        var item = (await ReadAsync<NewsResolveResponse>(await Client.GetAsync($"/api/v1/news/resolve?language=en&slug={slug}", Token))).Item!;
        Assert.Equal("https://publisher-sample.com/news/auto-1", item.OriginalUrl);
        Assert.False(item.Indexable);
        Assert.Empty(item.Alternates);
        var sitemap = await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/news/sitemap?language=en", Token));
        Assert.DoesNotContain(sitemap, entry => entry.Path == listed.Path);
        Assert.Contains(sitemap, entry => entry.Path == "/en/news");
        Assert.Contains(sitemap, entry => entry.Path == "/en/news/cybersecurity");
        Assert.Empty(await QueryAsync(db => db.SearchDocuments));

        // An editor marks it indexable: it joins the sitemap and the search index (group News).
        var details = await ItemAsync(owner, automatic.Id);
        await ReadAsync<NewsItemResponse>(await owner.PutAsJsonAsync($"{Items}/{automatic.Id}/indexing", new SetNewsIndexingRequest(true, details.Version), Token));
        await DeliverOutboxAsync();
        Assert.Contains(await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/news/sitemap?language=en", Token)), entry => entry.Path == listed.Path);
        var search = await ReadAsync<SiteSearchResponse>(await Client.GetAsync("/api/v1/search?language=en&q=agent%20sample%20headline", Token));
        Assert.Equal("News", Assert.Single(search.Groups).Group);
        Assert.Equal(listed.Path, Assert.Single(search.Groups[0].Items).Path);
    }

    [Fact]
    public async Task Failing_timing_out_or_copying_summary_services_leave_items_held()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        await CreateAllowedSourceAsync(owner, SourceRequest("Auto Publisher", FeedA, category.Id, autoPublish: true));
        Feeds.Serve(FeedA, Entry("story-1", "Sample story one"), Entry("story-2", "Sample story two"), Entry("story-3", "Sample story three"));
        var call = 0;
        Summaries = new FakeSummaryService(async (request, token) =>
        {
            switch (Interlocked.Increment(ref call))
            {
                case 1:
                    throw new HttpRequestException("agent unavailable");
                case 2:
                    await Task.Delay(TimeSpan.FromMinutes(5), token);   // exceeds the summary timeout
                    return Error.Failure("unreachable", "unreachable");
                default:
                    // Copies the source description verbatim: rejected by Onyx's own checks.
                    return new NewsSummaryDraft(
                    [
                        new NewsText("ar", "عنوان تجريبي", "ملخص تجريبي من الوكيل الخارجي لأغراض الاختبار فقط، وهو أطول من الحد الأدنى المطلوب."),
                        new NewsText("en", "Copied headline", $"{request.SourceExcerpt} plus a few more words to pass the length rule."),
                    ]);
            }
        });

        await IngestAsync(new Dictionary<string, string?> { ["News:SummaryTimeoutSeconds"] = "1" });

        var items = await QueryAsync(db => db.NewsItems.Include(item => item.Translations).OrderBy(item => item.OriginalUrl));
        Assert.All(items, item => Assert.Equal(NewsItemStatus.Held, item.Status));
        Assert.Equal([NewsHoldReason.SummaryUnavailable, NewsHoldReason.SummaryUnavailable, NewsHoldReason.SummaryRejected], items.Select(item => item.HoldReason!.Value));
        Assert.All(items, item => Assert.Empty(item.Translations));
        Assert.Empty((await ReadAsync<PublicNewsListResponse>(await Client.GetAsync("/api/v1/news?language=en", Token))).Items);
    }

    [Fact]
    public async Task One_failing_source_does_not_affect_the_others_and_notifies_once()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        var failing = await CreateAllowedSourceAsync(owner, SourceRequest("Failing Publisher", FeedA, category.Id));
        await CreateAllowedSourceAsync(owner, SourceRequest("Working Publisher", FeedB, category.Id));
        Feeds.Fail(FeedA, NewsFeedErrors.Timeout);
        Feeds.Serve(FeedB, Entry("ok-1", "Working sample story"));

        var outcome = await IngestAsync();

        Assert.False(outcome.WasSkipped);
        Assert.Contains("Sources 1 ok, 1 failed", outcome.Message, StringComparison.Ordinal);
        Assert.Equal(["Working Publisher"], await QueryAsync(db => db.NewsItems.Select(item => item.Publisher)));
        var source = (await QueryAsync(db => db.NewsSources.Where(s => s.Id == failing.Id))).Single();
        Assert.StartsWith(NewsFeedErrors.Timeout, source.LastError, StringComparison.Ordinal);
        Assert.DoesNotContain("publisher-sample.com", source.LastError, StringComparison.Ordinal);
        Assert.Equal(1, source.ConsecutiveFailures);
        var notification = Assert.Single(await QueryAsync(db => db.AdminNotifications.Where(n => n.Type == NotificationType.NewsIngestionFailed)));
        Assert.Equal("NewsSources.Manage", notification.RequiredPermission);

        // The failing source backs off; a later run with every due source failing fails the job (JobRun error + notification).
        Time.Advance(TimeSpan.FromHours(3));
        Feeds.Fail(FeedB, NewsFeedErrors.Malformed);
        await Assert.ThrowsAsync<InvalidOperationException>(IngestAsync);
        Assert.Single(await QueryAsync(db => db.AdminNotifications.Where(n => n.Type == NotificationType.NewsIngestionFailed && n.ResourceId == failing.Id.ToString())));
    }

    [Fact]
    public async Task Blocked_sources_are_never_fetched_and_similar_titles_are_held_for_an_editor()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        var blocked = await ReadAsync<NewsSourceResponse>(await owner.PostAsJsonAsync(Sources, SourceRequest("Blocked Publisher", FeedA, category.Id), Token));
        await CreateAllowedSourceAsync(owner, SourceRequest("Auto Publisher", FeedB, category.Id, autoPublish: true));
        Feeds.Serve(FeedA, Entry("blocked-1", "Should never be read"));
        Feeds.Serve(FeedB, Entry("a", "Same Sample Story"), Entry("b", "same sample story!"));
        Summaries = FakeSummaryService.Writing();

        await IngestAsync();

        Assert.DoesNotContain(FeedA, Feeds.Reads);
        Assert.Equal("Blocked", (await ReadAsync<NewsSourceResponse>(await owner.GetAsync($"{Sources}/{blocked.Id}", Token))).Status);
        var items = await QueryAsync(db => db.NewsItems.OrderBy(item => item.OriginalUrl));
        Assert.Equal(NewsItemStatus.Published, items[0].Status);
        Assert.Equal(NewsItemStatus.Held, items[1].Status);
        Assert.Equal(NewsHoldReason.SimilarItem, items[1].HoldReason);
        Assert.Equal(items[0].Id, items[1].SimilarToItemId);
    }
}
