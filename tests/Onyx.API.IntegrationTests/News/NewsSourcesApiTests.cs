using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.News;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.News;

public sealed class NewsSourcesApiTests(PostgresFixture fixture) : NewsApiTestBase(fixture)
{
    [Fact]
    public async Task Source_management_is_privileged()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        using var viewer = await SignInWithAsync("news-viewer", Permissions.News.View, Permissions.News.Edit, Permissions.News.Publish);
        var request = SourceRequest("Sample Publisher", "https://publisher-sample.com/feed.xml", category.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(Sources, Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.PostAsJsonAsync(Sources, request, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.GetAsync(Sources, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(Sources, request, Token)));

        using var manager = await SignInWithAsync("source-manager", Permissions.NewsSources.Manage);
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(manager.PostAsJsonAsync(Sources, request, Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(manager.GetAsync(Items, Token)));
    }

    [Theory]
    [InlineData("http://publisher-sample.com/feed.xml")]
    [InlineData("https://127.0.0.1/feed.xml")]
    [InlineData("https://[::1]/feed.xml")]
    [InlineData("https://localhost/feed.xml")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("https://user:secret@publisher-sample.com/feed.xml")]
    [InlineData("https://publisher-sample.com:8443/feed.xml")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public async Task Only_public_https_feed_urls_can_be_registered(string feedUrl)
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");

        using var response = await owner.PostAsJsonAsync(Sources, SourceRequest("Sample Publisher", feedUrl, category.Id), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("News.InvalidFeedUrl", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Empty(await QueryAsync(db => db.NewsSources));
    }

    [Fact]
    public async Task Sources_start_blocked_need_a_licensing_note_and_are_audited_without_their_feed_url()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني");
        const string feedUrl = "https://publisher-sample.com/feed.xml?key=sample-publisher-key";

        var created = await ReadAsync<NewsSourceResponse>(await owner.PostAsJsonAsync(Sources, SourceRequest("Sample Publisher", feedUrl, category.Id, note: null), Token));
        Assert.Equal("Blocked", created.Status);
        Assert.Null(created.NextFetchAtUtc);

        using (var refused = await owner.PostAsJsonAsync($"{Sources}/{created.Id}/allow", new NewsVersionRequest(created.Version), Token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("News.LicensingNoteRequired", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync(Sources, SourceRequest("Duplicate", feedUrl, category.Id), Token)));

        var noted = await ReadAsync<NewsSourceResponse>(await owner.PutAsJsonAsync(
            $"{Sources}/{created.Id}", SourceRequest("Sample Publisher", feedUrl, category.Id) with { Version = created.Version }, Token));
        var allowed = await ReadAsync<NewsSourceResponse>(await owner.PostAsJsonAsync($"{Sources}/{created.Id}/allow", new NewsVersionRequest(noted.Version), Token));
        Assert.Equal("Allowed", allowed.Status);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Sources}/{created.Id}/block", new NewsVersionRequest(created.Version), Token)));

        var audit = await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == created.Id.ToString()));
        Assert.Contains(audit, log => log.Action == AuditAction.NewsSourceCreated);
        Assert.Contains(audit, log => log.Action == AuditAction.NewsSourceAllowed);
        Assert.All(audit, log => Assert.DoesNotContain("sample-publisher-key", log.Changes ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Categories_are_admin_extensible_with_unique_slugs_and_the_launch_categories_are_seeded()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        using var viewer = await SignInWithAsync("news-viewer", Permissions.News.View);
        var category = await CreateCategoryAsync(owner, "cybersecurity", "Cybersecurity", "الأمن السيبراني", "Ransomware", "التصيّد");

        Assert.Equal(["ransomware", "التصيد"], category.Keywords);
        Assert.Equal("الأمن-السيبراني", category.Translations.Single(t => t.LanguageCode == "ar").Slug);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync(Categories, new SaveNewsCategoryRequest(
            "security-two", 2, null, [new NewsCategoryTranslationDto("ar", "أمن", null), new NewsCategoryTranslationDto("en", "Cybersecurity", null)], null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync(Categories, new SaveNewsCategoryRequest(
            "other", 2, null, [new NewsCategoryTranslationDto("ar", "أخرى", null), new NewsCategoryTranslationDto("en", "Other", null)], null), Token)));
        Assert.Single(await ReadAsync<List<NewsCategoryResponse>>(await viewer.GetAsync(Categories, Token)));

        // The migration seeds the four launch categories of architecture.md §17.3 (tests reset tables, so check the model's seed).
        await using var db = await DbAsync();
        var model = db.GetService<IDesignTimeModel>().Model;
        var seeded = model.FindEntityType(typeof(Domain.News.NewsCategory))!.GetSeedData().Select(row => (string)row["Key"]!).Order().ToList();
        Assert.Equal(["artificial-intelligence", "cybersecurity", "digital-marketing", "software-technology"], seeded);
        Assert.Equal(8, model.FindEntityType(typeof(Domain.News.NewsCategoryTranslation))!.GetSeedData().Count());
    }
}
