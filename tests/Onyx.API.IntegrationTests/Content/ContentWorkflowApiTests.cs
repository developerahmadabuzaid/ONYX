using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Content;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.Content;

public sealed class ContentWorkflowApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private static readonly string[] AllPagePermissions =
    [
        Permissions.Content.View, Permissions.Content.Create, Permissions.Content.Edit, Permissions.Content.SubmitForReview, Permissions.Content.Approve,
        Permissions.Content.Publish, Permissions.Pages.View, Permissions.Pages.Edit,
    ];

    [Fact]
    public async Task Submitting_approving_and_publishing_need_separate_permissions()
    {
        Fixture.RequireDatabase();
        using var editor = await SignInWithAsync("editor", Permissions.Content.View, Permissions.Content.Create, Permissions.Content.Edit, Permissions.Content.SubmitForReview, Permissions.Pages.View, Permissions.Pages.Edit);
        using var approver = await SignInWithAsync("approver", Permissions.Content.View, Permissions.Content.Approve, Permissions.Pages.View, Permissions.Pages.Edit);
        using var publisher = await SignInWithAsync("publisher", Permissions.Content.View, Permissions.Content.Publish, Permissions.Pages.View, Permissions.Pages.Edit);

        var page = await CreatePageAsync(editor);
        var submitted = await ReadAsync<ContentDetailResponse>(await editor.PostAsJsonAsync($"{Admin}/{page.Id}/submit", new ContentVersionRequest(page.Version), Token));
        Assert.Equal("PendingReview", submitted.Status);

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{Admin}/{page.Id}/approve", new ContentVersionRequest(submitted.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(approver.PostAsJsonAsync($"{Admin}/{page.Id}/publish", new ContentVersionRequest(submitted.Version), Token)));

        var approved = await ReadAsync<ContentDetailResponse>(await approver.PostAsJsonAsync($"{Admin}/{page.Id}/approve", new ContentVersionRequest(submitted.Version), Token));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{Admin}/{page.Id}/publish", new ContentVersionRequest(approved.Version), Token)));

        var published = await ReadAsync<ContentDetailResponse>(await publisher.PostAsJsonAsync($"{Admin}/{page.Id}/publish", new ContentVersionRequest(approved.Version), Token));
        Assert.True(published.IsLive);

        var english = await ResolveAsync("en", "about-us");
        var arabic = await ResolveAsync("ar", "صفحة-تعريفية");
        Assert.Equal("Content", english.Kind);
        Assert.Equal("Sample page", english.Content!.Title);
        Assert.Equal("صفحة تجريبية", arabic.Content!.Title);
        Assert.Equal("/ar/صفحة-تعريفية", arabic.Content.Seo.CanonicalPath);
        Assert.Equal("/en/about-us", arabic.Content.Seo.Alternates["en"]);
        Assert.Equal("/ar/صفحة-تعريفية", arabic.Content.Seo.XDefaultPath);

        var audit = await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == page.Id.ToString()).Select(log => log.Action));
        Assert.Contains(AuditAction.ContentSubmittedForReview, audit);
        Assert.Contains(AuditAction.ContentApproved, audit);
        Assert.Contains(AuditAction.ContentPublished, audit);
    }

    [Fact]
    public async Task Content_type_scope_is_enforced_server_side()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        using var servicesEditor = await SignInWithAsync("services", Permissions.Content.View, Permissions.Content.Edit, Permissions.Services.View, Permissions.Services.Edit);

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(servicesEditor.GetAsync($"{Admin}/{page.Id}", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(servicesEditor.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Hijack", "hijack", page.Version), Token)));
        var list = await ReadAsync<Onyx.Contracts.Common.PagedResult<ContentSummaryResponse>>(await servicesEditor.GetAsync(Admin, Token));
        Assert.DoesNotContain(list.Items, item => item.Id == page.Id);
    }

    [Fact]
    public async Task Drafts_and_edits_never_reach_the_public_site_until_published_again()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        Assert.Equal("NotFound", (await ResolveAsync("en", "about-us")).Kind);

        var published = await PublishAsync(owner, page);
        var edited = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Unreviewed title", "about-us", published.Version), Token));

        Assert.Equal("Draft", edited.Status);
        Assert.True(edited.IsLive);
        Assert.Equal("Sample page", (await ResolveAsync("en", "about-us")).Content!.Title);
    }

    [Fact]
    public async Task Slugs_are_unique_per_language_and_scope()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreatePageAsync(owner));
        var other = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(Admin, new CreateContentRequest("Page", null, null, null, 0), Token));

        using var sameLanguage = await owner.PutAsJsonAsync($"{Admin}/{other.Id}/translations/en", Translation("Other", "about-us", other.Version), Token);
        using var otherLanguage = await owner.PutAsJsonAsync($"{Admin}/{other.Id}/translations/ar", Translation("أخرى", "about-us", other.Version), Token);

        Assert.Equal(HttpStatusCode.Conflict, sameLanguage.StatusCode);
        Assert.Contains("Content.SlugTaken", await sameLanguage.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.True(otherLanguage.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Changing_a_slug_redirects_every_former_url_straight_to_the_current_one()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "first"));
        var second = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Sample page", "second", page.Version), Token));
        var republished = await PublishAsync(owner, second);
        var third = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Sample page", "third", republished.Version), Token));
        await PublishAsync(owner, third);

        var fromFirst = await ResolveAsync("en", "first");
        var fromSecond = await ResolveAsync("en", "second");

        Assert.Equal("Redirect", fromFirst.Kind);
        Assert.Equal("/en/third", fromFirst.RedirectPath);
        Assert.Equal("/en/third", fromSecond.RedirectPath);
        Assert.Equal("Content", (await ResolveAsync("en", "third")).Kind);
    }

    [Fact]
    public async Task Offline_content_answers_410_or_redirects_to_the_chosen_content_never_the_homepage()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var target = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "target", arSlug: "الهدف"));
        var page = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "retired", arSlug: "قديم"));

        var offline = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/unpublish", new TakeOfflineRequest("PendingDecision", null, page.Version), Token));
        Assert.Equal("PendingDecision", offline.OfflineResolution);
        Assert.Equal("Gone", (await ResolveAsync("en", "retired")).Kind);

        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/url-resolution", new TakeOfflineRequest("RedirectToContent", target.Id, offline.Version), Token));
        var redirected = await ResolveAsync("en", "retired");
        Assert.Equal("Redirect", redirected.Kind);
        Assert.Equal("/en/target", redirected.RedirectPath);
    }

    [Fact]
    public async Task Archived_content_is_not_public_and_not_in_the_sitemap()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreatePageAsync(owner));
        var sitemap = await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/content/sitemap?language=en", Token));
        Assert.Contains(sitemap, entry => entry.Path == "/en/about-us" && entry.Alternates["ar"] == "/ar/صفحة-تعريفية");

        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/archive", new TakeOfflineRequest("Gone", null, page.Version), Token));

        Assert.Equal("Gone", (await ResolveAsync("en", "about-us")).Kind);
        Assert.Empty(await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/content/sitemap?language=en", Token)));
    }

    [Fact]
    public async Task Noindex_pages_are_left_out_of_the_sitemap()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var seo = new SeoProfileDto(null, null, null, null, null, null, false, true, ["WebPage"], null, null, null, null, null, null);
        var updated = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(seo, page.Version), Token));
        await PublishAsync(owner, updated);

        var english = await ResolveAsync("en", "about-us");
        Assert.False(english.Content!.Seo.Index);
        Assert.DoesNotContain(await ReadAsync<List<SitemapEntryResponse>>(await Client.GetAsync("/api/v1/content/sitemap?language=en", Token)), entry => entry.Path == "/en/about-us");
    }

    [Fact]
    public async Task Seo_settings_reject_external_canonicals_and_oversized_values()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var external = new SeoProfileDto(null, null, null, null, null, "https://evil.example/page", true, true, null, null, null, null, null, null, null);
        var oversized = new SeoProfileDto(new string('k', 500), null, null, null, null, null, true, true, null, null, null, null, null, null, null);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(external, page.Version), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(oversized, page.Version), Token)));
    }

    [Fact]
    public async Task Scheduled_publishing_and_unpublishing_are_carried_out_by_the_scheduler_when_due()
    {
        Fixture.RequireDatabase();
        var owner = await SignInWithAsync("scheduler", AllPagePermissions);
        var page = await CreatePageAsync(owner);
        var submitted = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/submit", new ContentVersionRequest(page.Version), Token));
        var approved = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/approve", new ContentVersionRequest(submitted.Version), Token));
        var scheduled = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(
            $"{Admin}/{page.Id}/schedule", new ScheduleContentRequest(Time.GetUtcNow().AddHours(1), "Asia/Riyadh", approved.Version), Token));
        Assert.Equal("Scheduled", scheduled.Status);

        Assert.Empty(await InScopeAsync(services => services.GetRequiredService<IContentSchedulingService>().ListDuePublicationsAsync(Token)));
        Time.Advance(TimeSpan.FromHours(1));
        var due = await InScopeAsync(services => services.GetRequiredService<IContentSchedulingService>().ListDuePublicationsAsync(Token));
        Assert.True((await InScopeAsync(services => services.GetRequiredService<IContentSchedulingService>().PublishDueAsync(Assert.Single(due), Token))).IsSuccess);
        Assert.Equal("Content", (await ResolveAsync("en", "about-us")).Kind);

        // Access tokens are short-lived; sign in again after the clock moved.
        owner = AuthorizedClient((await SignInAsync("scheduler@onyx.test", StrongPassword)).AccessToken);
        var live = await ReadAsync<ContentDetailResponse>(await owner.GetAsync($"{Admin}/{page.Id}", Token));
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/unpublish-schedule", new ScheduleUnpublishRequest(Time.GetUtcNow().AddDays(1), live.Version), Token));
        Time.Advance(TimeSpan.FromDays(1));
        var dueOffline = await InScopeAsync(services => services.GetRequiredService<IContentSchedulingService>().ListDueUnpublicationsAsync(Token));
        Assert.True((await InScopeAsync(services => services.GetRequiredService<IContentSchedulingService>().UnpublishDueAsync(Assert.Single(dueOffline), Token))).IsSuccess);
        Assert.Equal("Gone", (await ResolveAsync("en", "about-us")).Kind);
    }

    [Fact]
    public async Task Preview_links_show_drafts_without_publishing_and_expire()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var link = await ReadAsync<PreviewLinkResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/preview-links", new CreatePreviewLinkRequest("en"), Token));

        using (var preview = await Client.GetAsync($"/api/v1/content/preview?token={link.Token}", Token))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Equal("no-store", preview.Headers.CacheControl?.ToString());
            var body = (await preview.Content.ReadFromJsonAsync<ContentPreviewResponse>(Token))!;
            Assert.Equal("Sample page", body.Content.Title);
            Assert.False(body.Content.Seo.Index);
        }

        Assert.Equal("NotFound", (await ResolveAsync("en", "about-us")).Kind);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync($"/api/v1/content/preview?token={link.Token[..^2]}xx", Token)));

        Time.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync($"/api/v1/content/preview?token={link.Token}", Token)));
        var stored = await QueryAsync(db => db.ContentPreviewTokens.Select(token => token.TokenHash));
        Assert.DoesNotContain(link.Token, stored);
    }

    [Fact]
    public async Task Restoring_a_revision_creates_a_new_draft_revision_and_keeps_history()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var changed = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Changed title", "about-us", page.Version), Token));
        var revisions = await ReadAsync<List<ContentRevisionSummaryResponse>>(await owner.GetAsync($"{Admin}/{page.Id}/revisions", Token));
        var original = revisions.Single(revision => revision.Number == page.CurrentRevisionNumber);

        var comparison = await ReadAsync<RevisionComparisonResponse>(await owner.GetAsync($"{Admin}/{page.Id}/revisions/compare?from={original.Number}&to={changed.CurrentRevisionNumber}", Token));
        Assert.Contains(comparison.Differences, difference => difference.LanguageCode == "en" && difference.After == "Changed title");

        var restored = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/revisions/{original.Number}/restore", new RestoreRevisionRequest(changed.Version, "Back"), Token));

        Assert.True(restored.CurrentRevisionNumber > changed.CurrentRevisionNumber);
        Assert.Equal("Sample page", restored.Translations.Single(translation => translation.LanguageCode == "en").Title);
        Assert.Equal("Draft", restored.Status);
        var after = await ReadAsync<List<ContentRevisionSummaryResponse>>(await owner.GetAsync($"{Admin}/{page.Id}/revisions", Token));
        Assert.Equal(revisions.Count + 1, after.Count);
        Assert.Contains(await QueryAsync(db => db.AuditLogs.Select(log => log.Action)), action => action == AuditAction.ContentRevisionRestored);
    }

    [Fact]
    public async Task A_stale_version_is_rejected_with_409()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("First writer", "about-us", page.Version), Token));

        using var stale = await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en", Translation("Second writer", "about-us", page.Version), Token);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Permanent_deletion_needs_confirmation_and_keeps_former_urls_answering()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await PublishAsync(owner, await CreatePageAsync(owner));
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{page.Id}/delete", new DeleteContentRequest(page.Id.ToString(), page.Version), Token)));

        var offline = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{page.Id}/unpublish", new TakeOfflineRequest("Gone", null, page.Version), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{page.Id}/delete", new DeleteContentRequest("yes", offline.Version), Token)));
        using var deleted = await owner.PostAsJsonAsync($"{Admin}/{page.Id}/delete", new DeleteContentRequest(page.Id.ToString(), offline.Version), Token);

        Assert.True(deleted.IsSuccessStatusCode);
        Assert.Equal("Gone", (await ResolveAsync("en", "about-us")).Kind);
        Assert.Empty(await QueryAsync(db => db.ContentItems.Where(item => item.Id == page.Id)));
    }
}
