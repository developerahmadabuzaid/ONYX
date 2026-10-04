using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Content;
using Onyx.Domain.Content;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>The one supported nesting depth (<see cref="ContentItem.MaxHierarchyDepth"/>) for creating, moving and re-publishing content.</summary>
public sealed class ContentHierarchyApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    [Fact]
    public async Task Content_can_be_nested_to_the_maximum_depth_but_not_one_level_deeper()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var chain = await CreateChainAsync(owner, ContentItem.MaxHierarchyDepth, publish: false);

        using var tooDeep = await owner.PostAsJsonAsync(Admin, new CreateContentRequest("Page", chain[^1].Id, null, null, 0), Token);

        Assert.Equal(ContentItem.MaxHierarchyDepth, chain.Count);
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
        Assert.Contains("Content.HierarchyTooDeep", await tooDeep.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Moving_a_subtree_counts_its_own_depth()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var chain = await CreateChainAsync(owner, ContentItem.MaxHierarchyDepth - 1, publish: false);
        var parent = await CreateAsync(owner, null, "subtree-root");
        var child = await CreateAsync(owner, parent.Id, "subtree-child");

        // Under the deepest page the two-level subtree would end one level beyond the maximum.
        using var tooDeep = await owner.PutAsJsonAsync($"{Admin}/{parent.Id}/settings", new UpdateContentSettingsRequest(chain[^1].Id, null, "Page", 0, null, null, parent.Version), Token);
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
        Assert.Contains("Content.HierarchyTooDeep", await tooDeep.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        // One level higher it ends exactly at the maximum.
        var moved = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{parent.Id}/settings", new UpdateContentSettingsRequest(chain[^2].Id, null, "Page", 0, null, null, parent.Version), Token));
        Assert.Equal(chain[^2].Id, moved.ParentId);
        Assert.NotNull(child);
    }

    [Fact]
    public async Task Content_cannot_be_moved_under_itself_or_its_descendants()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var chain = await CreateChainAsync(owner, 3, publish: false);
        var root = await ReadAsync<ContentDetailResponse>(await owner.GetAsync($"{Admin}/{chain[0].Id}", Token));

        using var underGrandchild = await owner.PutAsJsonAsync($"{Admin}/{root.Id}/settings", new UpdateContentSettingsRequest(chain[2].Id, null, "Page", 0, null, null, root.Version), Token);
        using var underItself = await owner.PutAsJsonAsync($"{Admin}/{root.Id}/settings", new UpdateContentSettingsRequest(root.Id, null, "Page", 0, null, null, root.Version), Token);

        Assert.Equal(HttpStatusCode.BadRequest, underGrandchild.StatusCode);
        Assert.Contains("own descendants", await underGrandchild.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, underItself.StatusCode);
    }

    [Fact]
    public async Task Renaming_the_top_page_moves_every_descendant_url_down_to_the_maximum_depth()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var chain = await CreateChainAsync(owner, ContentItem.MaxHierarchyDepth, publish: true);
        var deepestOldPath = string.Join('/', Enumerable.Range(1, ContentItem.MaxHierarchyDepth).Select(level => $"level-{level}"));
        Assert.Equal("Content", (await ResolveAsync("en", deepestOldPath)).Kind);

        var top = await ReadAsync<ContentDetailResponse>(await owner.GetAsync($"{Admin}/{chain[0].Id}", Token));
        var renamed = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{top.Id}/translations/en", Translation("Level 1", "renamed", top.Version), Token));
        await PublishAsync(owner, renamed);

        var deepestNewPath = "renamed/" + string.Join('/', Enumerable.Range(2, ContentItem.MaxHierarchyDepth - 1).Select(level => $"level-{level}"));
        Assert.Equal("Content", (await ResolveAsync("en", deepestNewPath)).Kind);
        var fromOld = await ResolveAsync("en", deepestOldPath);
        Assert.Equal("Redirect", fromOld.Kind);
        Assert.Equal("/en/" + deepestNewPath, fromOld.RedirectPath);
    }

    private static async Task<List<ContentDetailResponse>> CreateChainAsync(HttpClient owner, int levels, bool publish)
    {
        var chain = new List<ContentDetailResponse>();
        for (var level = 1; level <= levels; level++)
        {
            var created = await CreateAsync(owner, chain.Count == 0 ? null : chain[^1].Id, $"level-{level}");
            chain.Add(publish ? await PublishAsync(owner, created) : created);
        }

        return chain;
    }

    private static async Task<ContentDetailResponse> CreateAsync(HttpClient owner, Guid? parentId, string slug)
    {
        var created = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(Admin, new CreateContentRequest("Page", parentId, null, null, 0), Token));
        var arabic = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{created.Id}/translations/ar", Translation("صفحة", $"صفحة-{slug}", created.Version), Token));
        return await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{created.Id}/translations/en", Translation($"Page {slug}", slug, arabic.Version), Token));
    }
}
