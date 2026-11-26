using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>
/// Site content documents (homepage, navigation, site pages, website settings): permission checks, the draft → publish →
/// public flow with revisions, optimistic concurrency, validation and media references.
/// </summary>
public sealed class SiteContentApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private const string AdminSiteContent = "/api/v1/admin/site-content";
    private const string AdminSettings = "/api/v1/admin/site-settings";
    private static readonly string CloudPage = Uri.EscapeDataString(SiteContentKeys.Page("services.cloud"));
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Admin_endpoints_require_authentication_and_the_matching_permission()
    {
        Fixture.RequireDatabase();
        var viewer = await SignInWithAsync("viewer", Permissions.Pages.View);
        var editor = await SignInWithAsync("editor", Permissions.Pages.View, Permissions.Pages.Edit);

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(AdminSiteContent, Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("{}", null), Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync(AdminSettings, Token)));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(viewer.GetAsync($"{AdminSiteContent}/home", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("{}", null), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.PostAsJsonAsync($"{AdminSiteContent}/home/revisions/1/restore", new SiteContentVersionRequest(0), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{AdminSiteContent}/home/publish", new SiteContentVersionRequest(0), Token)));

        // Website settings need Settings.Manage and are never reachable through the page content endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.GetAsync(AdminSettings, Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(editor.GetAsync($"{AdminSiteContent}/settings", Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(editor.PutAsJsonAsync($"{AdminSiteContent}/settings/draft", new SaveSiteContentDraftRequest("{}", null), Token)));
    }

    [Fact]
    public async Task Drafts_stay_private_until_published_and_every_publish_keeps_a_restorable_revision()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        var draft = await ReadAsync<SiteContentDocumentResponse>(await owner.PutAsJsonAsync(
            $"{AdminSiteContent}/{CloudPage}/draft", new SaveSiteContentDraftRequest("""{"translations":{"en":{"h1":"First heading"}}}""", null), Token));
        Assert.DoesNotContain(SiteContentKeys.Page("services.cloud"), (await PublicAsync()).Documents.Keys);

        var first = await ReadAsync<SiteContentDocumentResponse>(await owner.PostAsJsonAsync($"{AdminSiteContent}/{CloudPage}/publish", new SiteContentVersionRequest(draft.Version), Token));
        Assert.Equal(1, first.PublishedRevision);
        Assert.Null(first.DraftJson);
        Assert.Contains("First heading", (await PublicAsync()).Documents[SiteContentKeys.Page("services.cloud")], StringComparison.Ordinal);

        var second = await ReadAsync<SiteContentDocumentResponse>(await owner.PutAsJsonAsync(
            $"{AdminSiteContent}/{CloudPage}/draft", new SaveSiteContentDraftRequest("""{"translations":{"en":{"h1":"Second heading"}}}""", first.Version), Token));
        second = await ReadAsync<SiteContentDocumentResponse>(await owner.PostAsJsonAsync($"{AdminSiteContent}/{CloudPage}/publish", new SiteContentVersionRequest(second.Version), Token));
        var revisions = await ReadAsync<List<SiteContentRevisionResponse>>(await owner.GetAsync($"{AdminSiteContent}/{CloudPage}/revisions", Token));
        Assert.Equal([2, 1], revisions.Select(revision => revision.Number));

        var restored = await ReadAsync<SiteContentDocumentResponse>(await owner.PostAsJsonAsync(
            $"{AdminSiteContent}/{CloudPage}/revisions/1/restore", new SiteContentVersionRequest(second.Version), Token));
        Assert.Contains("First heading", restored.DraftJson, StringComparison.Ordinal);
        Assert.Contains("Second heading", restored.PublishedJson, StringComparison.Ordinal);
        Assert.Contains("Second heading", (await PublicAsync()).Documents[SiteContentKeys.Page("services.cloud")], StringComparison.Ordinal);

        var discarded = await ReadAsync<SiteContentDocumentResponse>(await owner.PostAsJsonAsync($"{AdminSiteContent}/{CloudPage}/discard", new SiteContentVersionRequest(restored.Version), Token));
        Assert.Null(discarded.DraftJson);

        var actions = await QueryAsync(db => db.AuditLogs.Where(log => log.EntityType == "SiteContentDocument").Select(log => log.Action));
        Assert.Contains(AuditAction.SiteContentDraftSaved, actions);
        Assert.Equal(2, actions.Count(action => action == AuditAction.SiteContentPublished));
        Assert.Contains(AuditAction.SiteContentRevisionRestored, actions);
        Assert.Contains(AuditAction.SiteContentDraftDiscarded, actions);
    }

    [Fact]
    public async Task Stale_versions_and_blind_overwrites_are_refused()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var draft = await ReadAsync<SiteContentDocumentResponse>(await owner.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("{}", null), Token));
        await ReadAsync<SiteContentDocumentResponse>(await owner.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("""{"a":"b"}""", draft.Version), Token));

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("""{"a":"c"}""", draft.Version), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest("""{"a":"c"}""", null), Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{AdminSiteContent}/home/publish", new SiteContentVersionRequest(draft.Version), Token)));
    }

    [Theory]
    [InlineData("home", """{"h1":"<b>bold</b>"}""")]
    [InlineData("home", "[1,2]")]
    [InlineData("page:no-such page", "{}")]
    public async Task Markup_non_objects_and_unknown_keys_are_rejected(string key, string json)
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        var status = await StatusAsync(owner.PutAsJsonAsync($"{AdminSiteContent}/{Uri.EscapeDataString(key)}/draft", new SaveSiteContentDraftRequest(json, null), Token));

        Assert.True(status is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, status.ToString());
        Assert.Empty(await QueryAsync(db => db.SiteContentDocuments));
    }

    [Fact]
    public async Task Website_settings_are_validated_and_published_through_their_own_endpoint()
    {
        Fixture.RequireDatabase();
        var settingsManager = await SignInWithAsync("settings", Permissions.Settings.Manage);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(settingsManager.PutAsJsonAsync(
            $"{AdminSettings}/draft", new SaveSiteContentDraftRequest("""{"contactEmail":"not-an-email"}""", null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(settingsManager.PutAsJsonAsync(
            $"{AdminSettings}/draft", new SaveSiteContentDraftRequest("""{"socialLinks":[{"network":"linkedin","url":"http://insecure.example.test"}]}""", null), Token)));

        var draft = await ReadAsync<SiteContentDocumentResponse>(await settingsManager.PutAsJsonAsync(
            $"{AdminSettings}/draft", new SaveSiteContentDraftRequest("""{"contactEmail":"hello@example.test"}""", null), Token));
        await ReadAsync<SiteContentDocumentResponse>(await settingsManager.PostAsJsonAsync($"{AdminSettings}/publish", new SiteContentVersionRequest(draft.Version), Token));

        Assert.Contains("hello@example.test", (await PublicAsync()).Documents[SiteContentKeys.Settings], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Referenced_images_cannot_be_deleted_and_become_public_only_once_published()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var image = await UploadAsync(owner);
        var json = """{"settings":{"whyImageMediaId":"MEDIA"}}""".Replace("MEDIA", image.Id.ToString(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync(
            $"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest(json.Replace(image.Id.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal), null), Token)));
        var draft = await ReadAsync<SiteContentDocumentResponse>(await owner.PutAsJsonAsync($"{AdminSiteContent}/home/draft", new SaveSiteContentDraftRequest(json, null), Token));

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync($"/api/v1/media/{image.Id}", Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/media/{image.Id}", Token)));

        await ReadAsync<SiteContentDocumentResponse>(await owner.PostAsJsonAsync($"{AdminSiteContent}/home/publish", new SiteContentVersionRequest(draft.Version), Token));

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(Client.GetAsync($"/api/v1/media/{image.Id}", Token)));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/media/{image.Id}", Token)));
    }

    private async Task<PublicSiteContentResponse> PublicAsync() =>
        await ReadAsync<PublicSiteContentResponse>(await Client.GetAsync("/api/v1/site-content", Token));

    private static async Task<MediaAssetResponse> UploadAsync(HttpClient owner)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "photo.png");
        return await ReadAsync<MediaAssetResponse>(await owner.PostAsync("/api/v1/admin/media", form, Token));
    }
}
