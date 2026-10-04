using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Content;
using Onyx.Domain.Content;

namespace Onyx.API.IntegrationTests.Content;

public sealed class SeoFoundationApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    [Fact]
    public async Task Lastmod_moves_only_when_the_page_in_that_language_visibly_changes()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var published = await PublishAsync(owner, await CreatePageAsync(owner));
        var firstPublish = Time.GetUtcNow();
        Assert.Equal(firstPublish, await LastModifiedAsync("en", "/en/about-us"));

        // Re-ordering and re-publishing the same text is not a change.
        Time.Advance(TimeSpan.FromMinutes(3));
        var reordered = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{published.Id}/settings", new UpdateContentSettingsRequest(null, null, "Page", 5, null, null, published.Version), Token));
        var republished = await PublishAsync(owner, reordered);
        Assert.Equal(firstPublish, await LastModifiedAsync("en", "/en/about-us"));
        Assert.Equal(firstPublish, await LastModifiedAsync("ar", "/ar/من-نحن"));

        // An English edit moves only the English lastmod.
        Time.Advance(TimeSpan.FromMinutes(3));
        var edited = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{published.Id}/translations/en", Translation("Updated page", "about-us", republished.Version), Token));
        await PublishAsync(owner, edited);
        Assert.Equal(Time.GetUtcNow(), await LastModifiedAsync("en", "/en/about-us"));
        Assert.Equal(firstPublish, await LastModifiedAsync("ar", "/ar/من-نحن"));
        Assert.Equal(Time.GetUtcNow(), (await ResolveAsync("en", "about-us")).Content!.ModifiedAtUtc);
    }

    [Fact]
    public async Task Internal_seo_planning_does_not_move_lastmod_but_served_metadata_does()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var published = await PublishAsync(owner, await CreatePageAsync(owner));
        var firstPublish = Time.GetUtcNow();

        Time.Advance(TimeSpan.FromMinutes(3));
        var planning = new SeoProfileDto("cloud migration", ["cloud"], "Commercial", null, null, null, true, true, ["WebPage", "BreadcrumbList"], null, null, null, "SA", "Ready", "Internal note");
        var planned = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{published.Id}/translations/en/seo", new UpdateContentSeoRequest(planning, published.Version), Token));
        var republished = await PublishAsync(owner, planned);
        Assert.Equal(firstPublish, await LastModifiedAsync("en", "/en/about-us"));

        Time.Advance(TimeSpan.FromMinutes(3));
        var served = planning with { MetaDescription = "Who Onyx is and how the team works with clients." };
        var described = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{published.Id}/translations/en/seo", new UpdateContentSeoRequest(served, republished.Version), Token));
        await PublishAsync(owner, described);
        Assert.Equal(Time.GetUtcNow(), await LastModifiedAsync("en", "/en/about-us"));
        Assert.Equal(firstPublish, await LastModifiedAsync("ar", "/ar/من-نحن"));
    }

    [Fact]
    public async Task Canonicalized_pages_take_no_part_in_hreflang_in_html_or_sitemap()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var seo = new SeoProfileDto(null, null, null, null, null, "/ar/صفحة-أخرى", true, true, ["WebPage"], null, null, null, null, null, null);
        var updated = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/ar/seo", new UpdateContentSeoRequest(seo, page.Version), Token));
        await PublishAsync(owner, updated);

        var english = (await ResolveAsync("en", "about-us")).Content!;
        var arabic = (await ResolveAsync("ar", "من-نحن")).Content!;

        Assert.Equal(["en"], english.Seo.Alternates.Keys);
        Assert.Null(english.Seo.XDefaultPath);
        Assert.Empty(arabic.Seo.Alternates);
        Assert.Equal("/ar/صفحة-أخرى", arabic.Seo.CanonicalPath);
        var sitemap = await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/content/sitemap?language=en", Token));
        Assert.Equal(["en"], Assert.Single(sitemap).Alternates.Keys);
        Assert.Empty(await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/content/sitemap?language=ar", Token)));
    }

    [Fact]
    public async Task A_noindex_page_declares_no_alternates_and_is_not_listed_as_one()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var noindex = new SeoProfileDto(null, null, null, null, null, null, false, true, ["WebPage"], null, null, null, null, null, null);
        await PublishAsync(owner, await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(noindex, page.Version), Token)));

        Assert.Empty((await ResolveAsync("en", "about-us")).Content!.Seo.Alternates);
        Assert.Equal(["ar"], (await ResolveAsync("ar", "من-نحن")).Content!.Seo.Alternates.Keys);
    }

    [Fact]
    public async Task Publishing_moving_and_removing_urls_announce_exactly_the_changed_urls()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var published = await PublishAsync(owner, await CreatePageAsync(owner));
        Assert.Equal(["/ar/من-نحن", "/en/about-us"], (await AnnouncedAsync()).Single().Order(StringComparer.Ordinal));

        // Identical content re-published: nothing to announce.
        var reordered = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{published.Id}/settings", new UpdateContentSettingsRequest(null, null, "Page", 3, null, null, published.Version), Token));
        var republished = await PublishAsync(owner, reordered);
        Assert.Single(await AnnouncedAsync());

        // A slug change announces the old URL (now 301) and the new one.
        var renamed = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{published.Id}/translations/en", Translation("Sample page", "about-onyx", republished.Version), Token));
        var moved = await PublishAsync(owner, renamed);
        Assert.Equal(["/en/about-onyx", "/en/about-us"], (await AnnouncedAsync())[1].Order(StringComparer.Ordinal));

        // Going offline announces the current URLs (now 410).
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{published.Id}/unpublish", new TakeOfflineRequest("Gone", null, moved.Version), Token));
        Assert.Equal(["/ar/من-نحن", "/en/about-onyx"], (await AnnouncedAsync())[2].Order(StringComparer.Ordinal));

        // Manual redirect rules announce their source URL when created and when removed.
        var rule = await ReadAsync<RedirectResponse>(await owner.PostAsJsonAsync("/api/v1/admin/redirects", new SaveRedirectRequest("en", "legacy", "Gone", null, null, null, true), Token));
        Assert.Equal(["/en/legacy"], (await AnnouncedAsync())[3]);
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/redirects/{rule.Id}", Token)));
        Assert.Equal(["/en/legacy"], (await AnnouncedAsync())[4]);
    }

    [Fact]
    public async Task Organization_data_and_the_indexnow_key_are_unavailable_by_default()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/organization", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/search-engine-verification/indexnow-key", Token)));
    }

    private async Task<DateTimeOffset?> LastModifiedAsync(string language, string path)
    {
        var sitemap = await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync($"/api/v1/content/sitemap?language={language}", Token));
        return sitemap.Single(entry => entry.Path == path).LastModifiedUtc;
    }

    private async Task<List<List<string>>> AnnouncedAsync()
    {
        var name = typeof(PublicUrlsChanged).FullName;
        var payloads = await QueryAsync(db => db.OutboxMessages.Where(message => message.Type == name).OrderBy(message => message.OccurredAtUtc).ThenBy(message => message.Id).Select(message => message.Payload));
        return payloads
            .Select(payload => JsonDocument.Parse(payload).RootElement.GetProperty("paths").EnumerateArray().Select(path => path.GetString()!).ToList())
            .ToList();
    }
}
