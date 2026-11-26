using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;
using Onyx.Domain.Auditing;
using Onyx.Domain.Content;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>Projects, case studies, integrations, proof items and featured work: the proof guard and the 3-featured limit.</summary>
public sealed class WorkAndProofApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    private const string ProofItems = "/api/v1/admin/proof-items";

    [Fact]
    public async Task Unverified_proof_cannot_be_published_through_the_api()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var project = await CreatePageAsync(owner, "medical-records", "السجل-الطبي", "Project");
        var submitted = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{project.Id}/submit", new ContentVersionRequest(project.Version), Token));

        // Unverified proof is refused at approval...
        using (var refusedApproval = await owner.PostAsJsonAsync($"{Admin}/{project.Id}/approve", new ContentVersionRequest(submitted.Version), Token))
        {
            Assert.False(refusedApproval.IsSuccessStatusCode);
            Assert.Contains("Proof.NotVerified", await refusedApproval.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        // ...and at publishing, even when the verification is withdrawn after approval.
        var approved = await ApproveAsync(owner, await VerifyAsync(owner, submitted), submit: false);
        var revoked = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{project.Id}/proof-verification/revoke", new ContentVersionRequest(approved.Version), Token));
        Assert.Equal("Approved", revoked.Status);
        using (var refusedPublish = await owner.PostAsJsonAsync($"{Admin}/{project.Id}/publish", new ContentVersionRequest(revoked.Version), Token))
        {
            Assert.False(refusedPublish.IsSuccessStatusCode);
            Assert.Contains("Proof.NotVerified", await refusedPublish.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var verified = await VerifyAsync(owner, revoked);
        Assert.Equal("NotApplicable", verified.ProofVerification!.ClientPermission);
        var published = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{project.Id}/publish", new ContentVersionRequest(verified.Version), Token));

        Assert.True(published.IsLive);
        Assert.Equal("Content", (await ResolveAsync("en", "work/medical-records")).Kind);
        Assert.Equal("Content", (await ResolveAsync("ar", "أعمالنا/السجل-الطبي")).Kind);
        Assert.Contains(AuditAction.ProofVerified, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == project.Id.ToString()).Select(log => log.Action)));
    }

    [Fact]
    public async Task Verifying_proof_needs_the_proof_permission_and_any_edit_clears_the_verification()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        using var editor = await SignInWithAsync("projects", Permissions.Content.View, Permissions.Content.Edit, Permissions.Projects.View, Permissions.Projects.Edit);
        var project = await CreatePageAsync(owner, "sample-project", "مشروع-تجريبي", "Project");

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync(
            $"{Admin}/{project.Id}/proof-verification", new VerifyProofRequest("Self-verified", "NotApplicable", null, project.Version), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            $"{Admin}/{project.Id}/proof-verification", new VerifyProofRequest(" ", "NotApplicable", null, project.Version), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            $"{Admin}/{project.Id}/proof-verification", new VerifyProofRequest("Contract", "Granted", null, project.Version), Token)));

        var verified = await VerifyAsync(owner, project);
        var edited = await ReadAsync<ContentDetailResponse>(await editor.PutAsJsonAsync(
            $"{Admin}/{project.Id}/translations/en", Translation("Sample project, renamed", "sample-project", verified.Version), Token));

        Assert.Null(edited.ProofVerification);
    }

    [Fact]
    public async Task Live_proof_cannot_lose_its_verification_until_taken_offline()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var project = await PublishProjectAsync(owner, "sample-project", "مشروع-تجريبي");

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{project.Id}/proof-verification/revoke", new ContentVersionRequest(project.Version), Token)));

        var offline = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{project.Id}/unpublish", new TakeOfflineRequest("Gone", null, project.Version), Token));
        var revoked = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{project.Id}/proof-verification/revoke", new ContentVersionRequest(offline.Version), Token));
        Assert.Null(revoked.ProofVerification);
    }

    [Fact]
    public async Task A_case_study_belongs_to_a_live_project_and_lives_under_work_case_studies()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner, "about-us", "صفحة-تعريفية");

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(Admin, new CreateContentRequest("CaseStudy", null, null, null, 0), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(Admin, new CreateContentRequest("CaseStudy", page.Id, null, null, 0), Token)));

        var project = await CreatePageAsync(owner, "sample-project", "مشروع-تجريبي", "Project");
        var caseStudy = await CreatePageAsync(owner, "sample-story", "قصة-تجريبية", "CaseStudy", project.Id);
        var verifiedStory = await VerifyAsync(owner, caseStudy);
        var submittedStory = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{caseStudy.Id}/submit", new ContentVersionRequest(verifiedStory.Version), Token));
        using (var parentOffline = await owner.PostAsJsonAsync($"{Admin}/{caseStudy.Id}/approve", new ContentVersionRequest(submittedStory.Version), Token))
        {
            Assert.Contains("Content.ParentNotLive", await parentOffline.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var liveProject = await PublishProjectAsync(owner, project);
        var approved = await ApproveAsync(owner, submittedStory, submit: false);
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{caseStudy.Id}/publish", new ContentVersionRequest(approved.Version), Token));

        var resolved = await ResolveAsync("en", "work/case-studies/sample-story");
        Assert.Equal("Content", resolved.Kind);
        Assert.Contains(resolved.Content!.Breadcrumbs, crumb => crumb.Path == "/en/work/sample-project");
        Assert.Equal("Content", (await ResolveAsync("ar", "أعمالنا/دراسات-الحالة/قصة-تجريبية")).Kind);

        // Renaming the project does not move the case study, whose URL is in the shared case-studies section.
        var renamed = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{project.Id}/translations/en", Translation("Project", "renamed-project", liveProject.Version), Token));
        await PublishProjectAsync(owner, renamed);
        Assert.Equal("Content", (await ResolveAsync("en", "work/case-studies/sample-story")).Kind);
    }

    [Fact]
    public async Task At_most_three_live_verified_projects_can_be_featured_even_under_concurrent_requests()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var draft = await CreatePageAsync(owner, "draft-project", "مشروع-مسودة", "Project");
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{draft.Id}/featured", new SetFeaturedRequest(true, draft.Version), Token)));

        var projects = new List<ContentDetailResponse>();
        for (var index = 1; index <= 5; index++)
        {
            projects.Add(await PublishProjectAsync(owner, $"project-{index}", $"مشروع-{index}"));
        }

        for (var index = 0; index < 2; index++)
        {
            var featured = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{projects[index].Id}/featured", new SetFeaturedRequest(true, projects[index].Version), Token));
            Assert.Equal(index + 1, featured.FeaturedSlot);
        }

        // Two editors try to take the last slot at the same moment: exactly one succeeds.
        var attempts = await Task.WhenAll(
            owner.PostAsJsonAsync($"{Admin}/{projects[2].Id}/featured", new SetFeaturedRequest(true, projects[2].Version), Token),
            owner.PostAsJsonAsync($"{Admin}/{projects[3].Id}/featured", new SetFeaturedRequest(true, projects[3].Version), Token));
        Assert.Equal(1, attempts.Count(response => response.IsSuccessStatusCode));
        Assert.All(attempts.Where(response => !response.IsSuccessStatusCode), response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        foreach (var response in attempts)
        {
            response.Dispose();
        }

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{projects[4].Id}/featured", new SetFeaturedRequest(true, projects[4].Version), Token)));
        Assert.Equal(3, (await QueryAsync(db => db.FeaturedProjects)).Count);

        var homepage = await ReadAsync<List<ListingItemResponse>>(await Client.GetAsync("/api/v1/content/featured-projects?language=en", Token));
        Assert.Equal(3, homepage.Count);
        Assert.Equal("/en/work/project-1", homepage[0].Path);

        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{projects[0].Id}/featured", new SetFeaturedRequest(false, projects[0].Version), Token));
        var refilled = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{projects[4].Id}/featured", new SetFeaturedRequest(true, projects[4].Version), Token));
        Assert.Equal(1, refilled.FeaturedSlot);
        Assert.Contains(AuditAction.ProjectFeatured, await QueryAsync(db => db.AuditLogs.Select(log => log.Action)));
    }

    [Fact]
    public async Task Featuring_needs_the_projects_publish_permission()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        using var editor = await SignInWithAsync("projects", Permissions.Content.View, Permissions.Content.Edit, Permissions.Content.Publish, Permissions.Projects.View, Permissions.Projects.Edit);
        var project = await PublishProjectAsync(owner, "sample-project", "مشروع-تجريبي");

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync($"{Admin}/{project.Id}/featured", new SetFeaturedRequest(true, project.Version), Token)));
    }

    [Fact]
    public async Task Proof_items_appear_publicly_only_when_verified_and_published_and_leave_on_edit()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var project = await PublishProjectAsync(owner, "sample-project", "مشروع-تجريبي");
        var before = (await ResolveAsync("en", "work/sample-project")).Content!.ModifiedAtUtc;
        Time.Advance(TimeSpan.FromMinutes(3));

        var capability = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(ProofItems, new SaveProofItemRequest(
            project.Id, "Capability", [new("en", "Sample capability", "What it does."), new("ar", "قدرة تجريبية", null)], null, null, null, null, null, 0, null), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync($"{ProofItems}/{capability.Id}/publish", new ContentVersionRequest(capability.Version), Token)));
        Assert.Empty((await ResolveAsync("en", "work/sample-project")).Content!.Proof!.Capabilities);

        var verified = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(
            $"{ProofItems}/{capability.Id}/verify", new VerifyProofRequest("Checked in the delivered system", "NotApplicable", null, capability.Version), Token));
        var published = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync($"{ProofItems}/{capability.Id}/publish", new ContentVersionRequest(verified.Version), Token));

        var page = (await ResolveAsync("en", "work/sample-project")).Content!;
        Assert.Equal("Sample capability", Assert.Single(page.Proof!.Capabilities).Title);
        Assert.Equal("قدرة تجريبية", Assert.Single((await ResolveAsync("ar", "أعمالنا/مشروع-تجريبي")).Content!.Proof!.Capabilities).Title);
        Assert.True(page.ModifiedAtUtc > before);

        // Changing the wording takes it off the page until it is verified and published again.
        var edited = await ReadAsync<ProofItemResponse>(await owner.PutAsJsonAsync($"{ProofItems}/{capability.Id}", new SaveProofItemRequest(
            project.Id, "Capability", [new("en", "Changed capability", null)], null, null, null, null, null, 0, published.Version), Token));
        Assert.Equal("Draft", edited.Status);
        Assert.Null(edited.Verification);
        Assert.Empty((await ResolveAsync("en", "work/sample-project")).Content!.Proof!.Capabilities);
    }

    [Fact]
    public async Task Metrics_and_testimonials_carry_their_evidence_and_testimonials_need_client_permission()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var project = await CreatePageAsync(owner, "sample-project", "مشروع-تجريبي", "Project");

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(ProofItems, new SaveProofItemRequest(
            project.Id, "Metric", [new("en", "Sample metric", null)], "35%", null, null, null, null, 0, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(ProofItems, new SaveProofItemRequest(
            project.Id, "Testimonial", [new("en", "Sample quote", null)], null, null, null, null, null, 0, null), Token)));

        var metric = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(ProofItems, new SaveProofItemRequest(
            project.Id, "Metric", [new("en", "Sample metric", null)], "35%", new DateOnly(2026, 9, 1), null, null, null, 0, null), Token));
        Assert.Equal("35%", metric.Value);

        var testimonial = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(ProofItems, new SaveProofItemRequest(
            project.Id, "Testimonial", [new("en", "Sample quote", null)], null, null, "Sample Person", "Sample role", null, 0, null), Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            $"{ProofItems}/{testimonial.Id}/verify", new VerifyProofRequest("Email from the client", "NotApplicable", null, testimonial.Version), Token)));
        var verified = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(
            $"{ProofItems}/{testimonial.Id}/verify", new VerifyProofRequest("Email from the client", "Granted", "Written approval, 2026-09-30", testimonial.Version), Token));
        Assert.Equal("Granted", verified.Verification!.ClientPermission);

        // Notes are verification evidence, not audit values (they can name client contacts).
        var audited = await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == testimonial.Id.ToString()).Select(log => log.Changes));
        Assert.DoesNotContain(audited, changes => changes != null && changes.Contains("Written approval", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Testimonials_need_the_testimonial_permissions()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        using var editor = await SignInWithAsync("projects", Permissions.Content.View, Permissions.Content.Edit, Permissions.Content.Publish, Permissions.Projects.View, Permissions.Projects.Edit);
        var project = await CreatePageAsync(owner, "sample-project", "مشروع-تجريبي", "Project");
        var testimonial = new SaveProofItemRequest(project.Id, "Testimonial", [new("en", "Sample quote", null)], null, null, "Sample Person", null, null, 0, null);

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync(ProofItems, testimonial, Token)));
        var capability = await ReadAsync<ProofItemResponse>(await editor.PostAsJsonAsync(ProofItems, testimonial with { Kind = "Capability", AttributionName = null }, Token));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(editor.PostAsJsonAsync(
            $"{ProofItems}/{capability.Id}/verify", new VerifyProofRequest("Self", "NotApplicable", null, capability.Version), Token)));

        var created = await ReadAsync<ProofItemResponse>(await owner.PostAsJsonAsync(ProofItems, testimonial, Token));
        Assert.DoesNotContain(await ReadAsync<List<ProofItemResponse>>(await editor.GetAsync($"{ProofItems}?contentItemId={project.Id}", Token)), item => item.Id == created.Id);
    }

    [Fact]
    public async Task Service_pages_link_to_at_most_one_project_or_case_study()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var pillar = await CreatePageAsync(owner, "ai-solutions", "حلول-الذكاء-الاصطناعي", "ServicePillar");
        var first = await CreatePageAsync(owner, "first-project", "المشروع-الأول", "Project");
        var second = await CreatePageAsync(owner, "second-project", "المشروع-الثاني", "Project");

        await ReadAsync<ContentRelationResponse>(await owner.PostAsJsonAsync($"{Admin}/{pillar.Id}/relations", new AddContentRelationRequest(first.Id, "Related", 0, null), Token));
        using var second2 = await owner.PostAsJsonAsync($"{Admin}/{pillar.Id}/relations", new AddContentRelationRequest(second.Id, "Related", 1, null), Token);

        Assert.Equal(HttpStatusCode.Conflict, second2.StatusCode);
        Assert.Contains("Relation.OneWorkLink", await second2.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_work_listing_shows_live_projects_with_their_live_titles_only()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var live = await PublishProjectAsync(owner, "live-project", "مشروع-منشور");
        await CreatePageAsync(owner, "draft-project", "مشروع-مسودة", "Project");
        await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{live.Id}/translations/en", Translation("Unreviewed title", "live-project", live.Version), Token));

        var listing = await ReadAsync<PagedResult<ListingItemResponse>>(await Client.GetAsync("/api/v1/content/listing?language=en&section=work", Token));

        var item = Assert.Single(listing.Items);
        Assert.Equal("Sample page", item.Title);
        Assert.Equal("/en/work/live-project", item.Path);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/content/listing?language=en&section=competitors", Token)));
    }

    private static async Task<ContentDetailResponse> ApproveAsync(HttpClient client, ContentDetailResponse content, bool submit = true)
    {
        var submitted = submit
            ? await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{content.Id}/submit", new ContentVersionRequest(content.Version), Token))
            : content;
        return await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{content.Id}/approve", new ContentVersionRequest(submitted.Version), Token));
    }

    private static async Task<ContentDetailResponse> VerifyAsync(HttpClient client, ContentDetailResponse content) =>
        await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync(
            $"{Admin}/{content.Id}/proof-verification", new VerifyProofRequest("Confirmed by the owner from the delivered system", "NotApplicable", null, content.Version), Token));

    private static async Task<ContentDetailResponse> PublishProjectAsync(HttpClient client, string enSlug, string arSlug) =>
        await PublishProjectAsync(client, await CreatePageAsync(client, enSlug, arSlug, "Project"));

    private static async Task<ContentDetailResponse> PublishProjectAsync(HttpClient client, ContentDetailResponse project)
    {
        var approved = await ApproveAsync(client, await VerifyAsync(client, project));
        return await ReadAsync<ContentDetailResponse>(await client.PostAsJsonAsync($"{Admin}/{project.Id}/publish", new ContentVersionRequest(approved.Version), Token));
    }
}
