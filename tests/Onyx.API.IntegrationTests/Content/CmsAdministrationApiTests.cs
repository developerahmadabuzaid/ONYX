using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Brand;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;
using Onyx.Contracts.Knowledge;
using Onyx.Contracts.Media;
using Onyx.Contracts.Messaging;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.API.IntegrationTests.Content;

public sealed class CmsAdministrationApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    /// <summary>A valid 1×1 PNG.</summary>
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public static TheoryData<string, string> CmsEndpoints => new()
    {
        { "GET", Admin },
        { "POST", Admin },
        { "GET", $"{Admin}/{Guid.Empty}" },
        { "PUT", $"{Admin}/{Guid.Empty}/translations/en" },
        { "PUT", $"{Admin}/{Guid.Empty}/translations/en/seo" },
        { "POST", $"{Admin}/{Guid.Empty}/submit" },
        { "POST", $"{Admin}/{Guid.Empty}/approve" },
        { "POST", $"{Admin}/{Guid.Empty}/publish" },
        { "POST", $"{Admin}/{Guid.Empty}/archive" },
        { "POST", $"{Admin}/{Guid.Empty}/delete" },
        { "GET", $"{Admin}/{Guid.Empty}/revisions" },
        { "POST", $"{Admin}/{Guid.Empty}/preview-links" },
        { "GET", "/api/v1/admin/media" },
        { "POST", "/api/v1/admin/media" },
        { "DELETE", $"/api/v1/admin/media/{Guid.Empty}" },
        { "GET", "/api/v1/admin/redirects" },
        { "POST", "/api/v1/admin/redirects" },
        { "GET", "/api/v1/admin/ctas" },
        { "GET", "/api/v1/admin/clusters" },
        { "GET", "/api/v1/admin/brand-kit" },
        { "PUT", "/api/v1/admin/brand-kit" },
        { "GET", "/api/v1/admin/knowledge" },
        { "POST", $"/api/v1/admin/knowledge/{Guid.Empty}/approve" },
        { "GET", "/api/v1/admin/keywords" },
        { "GET", "/api/v1/admin/competitors" },
        { "GET", "/api/v1/admin/seo-recommendations" },
        { "POST", $"/api/v1/admin/seo-recommendations/{Guid.Empty}/approve" },
        { "GET", "/api/v1/admin/content-calendar" },
        { "GET", "/api/v1/admin/intelligence/capabilities" },
        { "GET", "/api/v1/admin/email-templates" },
        { "POST", $"{Admin}/{Guid.Empty}/proof-verification" },
        { "POST", $"{Admin}/{Guid.Empty}/proof-verification/revoke" },
        { "POST", $"{Admin}/{Guid.Empty}/featured" },
        { "GET", $"/api/v1/admin/proof-items?contentItemId={Guid.Empty}" },
        { "POST", "/api/v1/admin/proof-items" },
        { "PUT", $"/api/v1/admin/proof-items/{Guid.Empty}" },
        { "POST", $"/api/v1/admin/proof-items/{Guid.Empty}/verify" },
        { "POST", $"/api/v1/admin/proof-items/{Guid.Empty}/publish" },
        { "DELETE", $"/api/v1/admin/proof-items/{Guid.Empty}" },
        { "GET", "/api/v1/admin/authors" },
        { "POST", "/api/v1/admin/authors" },
        { "DELETE", $"/api/v1/admin/authors/{Guid.Empty}" },
    };

    [Theory]
    [MemberData(nameof(CmsEndpoints))]
    public async Task Cms_endpoints_reject_anonymous_callers_with_401(string method, string path)
    {
        Fixture.RequireDatabase();

        using var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CmsEndpoints))]
    public async Task Cms_endpoints_reject_users_without_the_permission_with_403(string method, string path)
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        using var sales = AuthorizedClient((await SignInAsync("sales@onyx.test", StrongPassword)).AccessToken);

        // Uploads must be multipart; any other body is refused by content type before authorization is even relevant.
        HttpContent content = path.EndsWith("/media", StringComparison.Ordinal) && method == "POST" ? Form(Png, "photo.png", "image/png") : JsonContent.Create(new { });
        using var response = await sales.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = content }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Uploads_are_verified_by_content_and_svg_or_disguised_files_are_rejected()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        var asset = await UploadAsync(owner, Png, "photo.png", "image/png");
        Assert.Equal("image/png", asset.ContentType);
        Assert.Equal(1, asset.Width);

        Assert.Equal(HttpStatusCode.BadRequest, await UploadStatusAsync(owner, Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"), "logo.svg", "image/svg+xml"));
        Assert.Equal(HttpStatusCode.BadRequest, await UploadStatusAsync(owner, Encoding.UTF8.GetBytes("MZ not an image at all"), "photo.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, await UploadStatusAsync(owner, [], "empty.png", "image/png"));
    }

    [Fact]
    public async Task Unused_media_is_not_public_and_used_media_cannot_be_deleted()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var asset = await UploadAsync(owner, Png, "../../etc/passwd.png", "image/png");
        Assert.DoesNotContain("..", asset.FileName, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync($"/api/v1/media/{asset.Id}", Token)));

        var page = await CreatePageAsync(owner);
        var withImage = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{page.Id}/settings", new UpdateContentSettingsRequest(null, null, "Page", 0, null, asset.Id, page.Version), Token));
        await PublishAsync(owner, withImage);

        using (var media = await Client.GetAsync($"/api/v1/media/{asset.Id}", Token))
        {
            Assert.Equal(HttpStatusCode.OK, media.StatusCode);
            Assert.Equal("image/png", media.Content.Headers.ContentType?.MediaType);
        }

        var usages = await ReadAsync<List<MediaUsageResponse>>(await owner.GetAsync($"/api/v1/admin/media/{asset.Id}/usages", Token));
        Assert.NotEmpty(usages);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/media/{asset.Id}", Token)));
    }

    [Fact]
    public async Task The_brand_kit_starts_empty_and_needs_the_manage_permission_to_change()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("marketing@onyx.test", StrongPassword, SystemRoles.Marketing);
        using var marketing = AuthorizedClient((await SignInAsync("marketing@onyx.test", StrongPassword)).AccessToken);

        var kit = await ReadAsync<BrandKitResponse>(await marketing.GetAsync("/api/v1/admin/brand-kit", Token));
        Assert.Null(kit.OfficialLogoMediaId);
        Assert.Null(kit.WebsiteUrl);
        Assert.Empty(kit.Colors);
        Assert.NotEmpty(kit.MissingItems);

        var update = new UpdateBrandKitRequest(null, null, null, [], [], true, true, null, kit.Version);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(marketing.PutAsJsonAsync("/api/v1/admin/brand-kit", update, Token)));
    }

    [Fact]
    public async Task Knowledge_is_approved_only_by_approvers_and_only_with_a_source()
    {
        Fixture.RequireDatabase();
        using var editor = await SignInWithAsync("writer", Permissions.Knowledge.View, Permissions.Knowledge.Edit);
        using var approver = await SignInWithAsync("verifier", Permissions.Knowledge.View, Permissions.Knowledge.Approve);
        var entry = await ReadAsync<KnowledgeEntryResponse>(await editor.PostAsJsonAsync(
            "/api/v1/admin/knowledge", new SaveKnowledgeEntryRequest("CompanyInformation", "Sample fact", [new LocalizedTextDto("en", "Sample statement for tests.")], null, null), Token));

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"/api/v1/admin/knowledge/{entry.Id}/approve", new KnowledgeVersionRequest(entry.Version), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(approver.PostAsJsonAsync($"/api/v1/admin/knowledge/{entry.Id}/approve", new KnowledgeVersionRequest(entry.Version), Token)));

        var sourced = await ReadAsync<KnowledgeEntryResponse>(await editor.PutAsJsonAsync(
            $"/api/v1/admin/knowledge/{entry.Id}", new SaveKnowledgeEntryRequest("CompanyInformation", "Sample fact", [new LocalizedTextDto("en", "Sample statement for tests.")], "Test source", entry.Version), Token));
        var approved = await ReadAsync<KnowledgeEntryResponse>(await approver.PostAsJsonAsync($"/api/v1/admin/knowledge/{entry.Id}/approve", new KnowledgeVersionRequest(sourced.Version), Token));
        Assert.Equal("Approved", approved.Status);
    }

    [Fact]
    public async Task Email_copy_cannot_drop_required_placeholders_or_inject_unknown_ones()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var templates = await ReadAsync<List<EmailTemplateResponse>>(await owner.GetAsync("/api/v1/admin/email-templates", Token));
        var reset = templates.Single(template => template.Key == PasswordResetEmailTemplate.TemplateKey);
        var required = reset.RequiredPlaceholders.First();
        var path = $"/api/v1/admin/email-templates/{reset.Key}/en";

        var missing = new SaveEmailTemplateCopyRequest("Subject", "Heading", ["No placeholder here."], "Continue", null, null);
        var unknown = new SaveEmailTemplateCopyRequest("Subject", "Heading {token}", [$"Valid for {{{required}}}."], "Continue", null, null);
        var valid = new SaveEmailTemplateCopyRequest("Subject", "Heading", [$"Valid for {{{required}}}."], "Continue", null, null);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync(path, missing, Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync(path, unknown, Token)));
        var saved = await ReadAsync<EmailTemplateResponse>(await owner.PutAsJsonAsync(path, valid, Token));
        Assert.True(saved.Copies.Single(copy => copy.LanguageCode == "en").IsCustomized);
    }

    [Fact]
    public async Task Manual_redirects_reject_external_targets_and_loops()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/redirects", new SaveRedirectRequest("en", "old", "Permanent", null, "https://evil.example/x", null, true), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/redirects", new SaveRedirectRequest("en", "old", "Permanent", null, "//evil.example", null, true), Token)));
        await ReadAsync<RedirectResponse>(await owner.PostAsJsonAsync("/api/v1/admin/redirects", new SaveRedirectRequest("en", "a", "Permanent", null, "b", null, true), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/redirects", new SaveRedirectRequest("en", "b", "Permanent", null, "a", null, true), Token)));

        await ReadAsync<RedirectResponse>(await owner.PostAsJsonAsync("/api/v1/admin/redirects", new SaveRedirectRequest("en", "legacy", "Gone", null, null, null, true), Token));
        Assert.Equal("Gone", (await ResolveAsync("en", "legacy")).Kind);
    }

    [Fact]
    public async Task Manual_recommendations_cannot_carry_metrics_and_data_sources_report_not_connected()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        var capabilities = await ReadAsync<IntelligenceCapabilitiesResponse>(await owner.GetAsync("/api/v1/admin/intelligence/capabilities", Token));
        Assert.All(capabilities.Sources, source => Assert.Equal("NotConfigured", source.Status));

        var recommendation = await ReadAsync<RecommendationResponse>(await owner.PostAsJsonAsync("/api/v1/admin/seo-recommendations",
            new ProposeRecommendationRequest("MetaTitle", "Shorter meta title", "The title is long.", null, [new ProposedChangeDto("metaTitle", null, "Shorter")], null, "en", null), Token));
        Assert.Equal("Editor", recommendation.Origin);
        Assert.NotEqual("Evidence", recommendation.Basis);
        Assert.All(recommendation.Evidence, evidence => Assert.Null(evidence.Metrics));

        var keywords = await ReadAsync<PagedResult<KeywordTargetResponse>>(await owner.GetAsync("/api/v1/admin/keywords", Token));
        Assert.Empty(keywords.Items);
    }

    private static async Task<MediaAssetResponse> UploadAsync(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        using var form = Form(bytes, fileName, contentType);
        return await ReadAsync<MediaAssetResponse>(await client.PostAsync("/api/v1/admin/media", form, Token));
    }

    private static async Task<HttpStatusCode> UploadStatusAsync(HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        using var form = Form(bytes, fileName, contentType);
        return await StatusAsync(client.PostAsync("/api/v1/admin/media", form, Token));
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }
}
