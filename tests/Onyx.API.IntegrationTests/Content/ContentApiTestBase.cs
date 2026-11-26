using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Identity;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Content;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>Helpers for CMS tests: users with exact permission sets and a full create → review → publish flow through the API.</summary>
public abstract class ContentApiTestBase(PostgresFixture fixture) : IdentityApiTestBase(fixture), IDisposable
{
    protected const string Admin = "/api/v1/admin/content";

    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "onyx-media-tests", Guid.NewGuid().ToString("N"));

    /// <summary>This test class's own media storage directory (deleted afterwards).</summary>
    protected string MediaRoot => _mediaRoot;

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["RateLimiting:Strict:PermitLimit"] = "10000",
        ["RateLimiting:Global:PermitLimit"] = "10000",
        ["Media:StorageRoot"] = _mediaRoot,
    };

    public void Dispose()
    {
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>A non-privileged user holding exactly <paramref name="permissions"/> (no 2FA needed), signed in.</summary>
    protected async Task<HttpClient> SignInWithAsync(string name, params string[] permissions)
    {
        await CreateRoleAsync($"role-{name}", permissions);
        var email = $"{name}@onyx.test";
        await CreateUserAsync(email, StrongPassword, $"role-{name}");
        return AuthorizedClient((await SignInAsync(email, StrongPassword)).AccessToken);
    }

    protected static async Task<ContentDetailResponse> CreatePageAsync(HttpClient client, string enSlug = "about-us", string arSlug = "صفحة-تعريفية", string type = "Page", Guid? parentId = null)
    {
        var created = await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync(Admin, new CreateContentRequest(type, parentId, null, null, 0), Token));
        var withArabic = await ReadAsync<ContentDetailResponse>(await client.PutAsJsonAsync(
            $"{Admin}/{created.Id}/translations/ar", Translation("صفحة تجريبية", arSlug, created.Version), Token));
        return await ReadAsync<ContentDetailResponse>(await client.PutAsJsonAsync(
            $"{Admin}/{created.Id}/translations/en", Translation("Sample page", enSlug, withArabic.Version), Token));
    }

    protected static UpdateContentTranslationRequest Translation(string title, string slug, uint version) =>
        new(title, $"{title} summary.", slug, [new ContentBlockDto("Paragraph", Spans: [new TextSpanDto("Sample paragraph for tests.")])], [], version);

    protected static async Task<ContentDetailResponse> PublishAsync(HttpClient client, ContentDetailResponse content)
    {
        var submitted = await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{content.Id}/submit", new ContentVersionRequest(content.Version), Token));
        var approved = await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{content.Id}/approve", new ContentVersionRequest(submitted.Version), Token));
        return await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{content.Id}/publish", new ContentVersionRequest(approved.Version), Token));
    }

    protected async Task<ContentResolveResponse> ResolveAsync(string language, string path) =>
        await ReadAsync<ContentResolveResponse>(await Client.GetAsync($"/api/v1/content/resolve?language={language}&path={Uri.EscapeDataString(path)}", Token));

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(Token);
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
            return System.Text.Json.JsonSerializer.Deserialize<T>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        }
    }

    protected static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }
}
