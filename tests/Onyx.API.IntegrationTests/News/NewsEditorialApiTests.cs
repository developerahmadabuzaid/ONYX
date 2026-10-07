using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.News;
using Onyx.Contracts.Search;
using Onyx.Domain.Auditing;
using Onyx.Domain.News;

namespace Onyx.API.IntegrationTests.News;

/// <summary>Editors turn held items into public ones: permission boundaries, the summary rule, take-down and concurrency.</summary>
public sealed class NewsEditorialApiTests(PostgresFixture fixture) : NewsApiTestBase(fixture)
{
    private const string Feed = "https://publisher-sample.com/feed.xml";

    [Fact]
    public async Task Each_action_needs_its_own_permission()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var item = await HeldItemAsync(owner);
        using var viewer = await SignInWithAsync("news-viewer", Permissions.News.View);
        using var editor = await SignInWithAsync("news-editor", Permissions.News.View, Permissions.News.Edit);
        using var other = await SignInWithAsync("content-only", Permissions.Content.View);
        var text = new UpdateNewsTranslationRequest("Sample headline", "A sample Onyx summary written for tests only, longer than the minimum.", item.Version);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(Items, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(other.GetAsync(Items, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(other.GetAsync($"{Items}/{item.Id}", Token)));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(viewer.GetAsync($"{Items}/{item.Id}", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PutAsJsonAsync($"{Items}/{item.Id}/translations/en", text, Token)));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(editor.PutAsJsonAsync($"{Items}/{item.Id}/translations/en", text, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(item.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{Items}/{item.Id}/delete", new NewsVersionRequest(item.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PutAsJsonAsync($"{Items}/{item.Id}/indexing", new SetNewsIndexingRequest(true, item.Version), Token)));
    }

    [Fact]
    public async Task No_summary_no_publication_then_a_published_item_carries_its_attribution_in_both_languages()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var item = await HeldItemAsync(owner);

        using (var refused = await owner.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(item.Version), Token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("News.SummaryRequired", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var written = await WriteSummariesAsync(owner, item);
        Time.Advance(TimeSpan.FromMinutes(5));
        var published = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(written.Version), Token));
        Assert.Equal("Published", published.Status);
        Assert.False(published.AutoPublished);
        Assert.Contains(AuditAction.NewsItemPublished, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == item.Id.ToString()).Select(log => log.Action)));

        var english = (await ReadAsync<NewsResolveResponse>(await Client.GetAsync("/api/v1/news/resolve?language=en&slug=sample-headline-for-tests", Token))).Item!;
        Assert.Equal("Sample headline for tests", english.Title);
        Assert.Equal("Sample Publisher", english.Publisher);
        Assert.Equal("https://publisher-sample.com/news/story-1", english.OriginalUrl);
        Assert.Equal(item.SourcePublishedAtUtc, english.SourcePublishedAtUtc);
        Assert.Equal(Time.GetUtcNow(), english.PublishedAtUtc);
        Assert.NotEqual(english.SourcePublishedAtUtc, english.PublishedAtUtc);
        Assert.Equal("/en/news/sample-headline-for-tests", english.Path);
        var arabicSlug = published.Translations.Single(t => t.LanguageCode == "ar").Slug;
        var arabic = (await ReadAsync<NewsResolveResponse>(await Client.GetAsync($"/api/v1/news/resolve?language=ar&slug={Uri.EscapeDataString(arabicSlug)}", Token))).Item!;
        Assert.Equal("عنوان تجريبي للاختبار", arabic.Title);
        Assert.Equal($"/ar/آخر-الأخبار/{arabicSlug}", arabic.Path);

        // The slug is fixed once public: a later headline edit keeps the URL.
        var renamed = await ReadAsync<NewsItemResponse>(await owner.PutAsJsonAsync($"{Items}/{item.Id}/translations/en", new UpdateNewsTranslationRequest(
            "A different headline", "A sample Onyx summary written for tests only, longer than the minimum length.", published.Version), Token));
        Assert.Equal("sample-headline-for-tests", renamed.Translations.Single(t => t.LanguageCode == "en").Slug);
    }

    [Fact]
    public async Task Hidden_unpublished_and_deleted_items_leave_every_public_surface_and_are_never_reingested()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var item = await WriteSummariesAsync(owner, await HeldItemAsync(owner));
        var published = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(item.Version), Token));
        var indexable = await ReadAsync<NewsItemResponse>(await owner.PutAsJsonAsync($"{Items}/{item.Id}/indexing", new SetNewsIndexingRequest(true, published.Version), Token));
        await DeliverOutboxAsync();
        Assert.Single(await QueryAsync(db => db.SearchDocuments.Where(document => document.ContentItemId == item.Id && document.LanguageCode == "en")));

        var hidden = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/hide", new NewsVersionRequest(indexable.Version), Token));
        await DeliverOutboxAsync();
        Assert.Equal("Hidden", hidden.Status);
        Assert.Null((await ReadAsync<NewsResolveResponse>(await Client.GetAsync("/api/v1/news/resolve?language=en&slug=sample-headline-for-tests", Token))).Item);
        Assert.Empty(await QueryAsync(db => db.SearchDocuments));
        Assert.Empty((await ReadAsync<SiteSearchResponse>(await Client.GetAsync("/api/v1/search?language=en&q=sample%20headline", Token))).Groups);

        var republished = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(hidden.Version), Token));
        var unpublished = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/unpublish", new NewsVersionRequest(republished.Version), Token));
        Assert.Empty((await ReadAsync<PublicNewsListResponse>(await Client.GetAsync("/api/v1/news?language=en", Token))).Items);

        var deleted = await ReadAsync<NewsItemResponse>(await owner.PostAsJsonAsync($"{Items}/{item.Id}/delete", new NewsVersionRequest(unpublished.Version), Token));
        Assert.Equal("Deleted", deleted.Status);
        Assert.Empty(deleted.Translations);
        Assert.Contains(AuditAction.NewsItemDeleted, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == item.Id.ToString()).Select(log => log.Action)));

        // The tombstone keeps the article from being ingested again.
        Time.Advance(TimeSpan.FromHours(2));
        await IngestAsync();
        Assert.Single(await QueryAsync(db => db.NewsItems));
        Assert.Equal(NewsItemStatus.Deleted, (await QueryAsync(db => db.NewsItems)).Single().Status);
    }

    [Fact]
    public async Task Stale_versions_are_rejected_and_public_requests_are_validated()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var item = await WriteSummariesAsync(owner, await HeldItemAsync(owner));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(owner.PostAsJsonAsync($"{Items}/{item.Id}/hide", new NewsVersionRequest(item.Version), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Items}/{item.Id}/publish", new NewsVersionRequest(item.Version), Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(owner.GetAsync($"{Items}/{Guid.CreateVersion7()}", Token)));

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.GetAsync("/api/v1/news?language=fr", Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.GetAsync("/api/v1/news/resolve?language=de&slug=x", Token)));
        var unknown = await ReadAsync<NewsResolveResponse>(await Client.GetAsync("/api/v1/news/resolve?language=en&slug=no-such-item", Token));
        Assert.Null(unknown.Item);
        Assert.Null(unknown.List);
        var traversal = await ReadAsync<NewsResolveResponse>(await Client.GetAsync($"/api/v1/news/resolve?language=en&slug={Uri.EscapeDataString("../../admin")}", Token));
        Assert.Null(traversal.Item);
        Assert.Null(traversal.List);
    }

    private async Task<NewsItemResponse> HeldItemAsync(HttpClient owner)
    {
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        await CreateAllowedSourceAsync(owner, SourceRequest("Sample Publisher", Feed, category.Id));
        Feeds.Serve(Feed, Entry("story-1", "Sample story one", Time.GetUtcNow().AddHours(-6)));
        await IngestAsync();
        var held = (await QueryAsync(db => db.NewsItems)).Single();
        return await ItemAsync(owner, held.Id);
    }
}
