using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Brand;
using Onyx.Contracts.Media;
using Onyx.Contracts.Organization;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>A launched configuration: production indexing on, IndexNow key set and the owner's Organization switch on.</summary>
public sealed class SearchEngineActivationApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private const string IndexNowKey = "0123456789abcdef0123456789abcdef";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Site:PublicBaseUrl"] = "https://www.onyx.test",
        ["Site:AllowIndexing"] = "true",
        ["IndexNow:Key"] = IndexNowKey,
        ["Seo:OrganizationStructuredDataEnabled"] = "true",
    };

    [Fact]
    public async Task The_indexnow_key_is_served_once_configured_and_launched()
    {
        Fixture.RequireDatabase();

        using var response = await Client.GetAsync("/api/v1/search-engine-verification/indexnow-key", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(IndexNowKey, await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Organization_data_needs_the_verified_logo_and_this_sites_own_website()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/organization", Token)));

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "logo.png");
        var logo = await ReadAsync<MediaAssetResponse>(await owner.PostAsync("/api/v1/admin/media", form, Token));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync($"/api/v1/media/{logo.Id}", Token)));

        // Another site's address is not this organization's verified website.
        var kit = await ReadAsync<BrandKitResponse>(await owner.GetAsync("/api/v1/admin/brand-kit", Token));
        var mismatch = await ReadAsync<BrandKitResponse>(await owner.PutAsJsonAsync(
            "/api/v1/admin/brand-kit", new UpdateBrandKitRequest(logo.Id, "https://other.example", null, [], [], false, false, null, kit.Version), Token));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/organization", Token)));

        await ReadAsync<BrandKitResponse>(await owner.PutAsJsonAsync(
            "/api/v1/admin/brand-kit", new UpdateBrandKitRequest(logo.Id, "https://www.onyx.test", null, [], [], false, false, null, mismatch.Version), Token));
        var organization = await ReadAsync<PublicOrganizationResponse>(await Client.GetAsync("/api/v1/organization", Token));

        Assert.Equal("Onyx Vanguard Solutions", organization.Name);
        Assert.Equal("https://www.onyx.test/", organization.Url);
        Assert.Equal(logo.Id, organization.LogoMediaId);
        Assert.Null(organization.Email);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(Client.GetAsync($"/api/v1/media/{logo.Id}", Token)));
    }
}
