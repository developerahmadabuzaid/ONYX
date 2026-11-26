using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Identity;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Insights;
using Onyx.Domain.Auditing;
using Onyx.Domain.Content;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>Insights (AI-approval guard, byline, scheduling), topics, glossary terms and the integrations showcase.</summary>
public sealed class InsightsApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    [Fact]
    public async Task Ai_assisted_insights_keep_their_label_and_publish_only_after_a_recorded_human_approval()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var insight = await CreatePageAsync(owner, "choosing-a-crm", "اختيار-نظام-crm", "Insight");
        var assisted = await SettingsAsync(owner, insight, "Article", "AiAssisted", null);
        Assert.Equal("AiAssisted", assisted.Origin);

        using (var downgrade = await owner.PutAsJsonAsync($"{Admin}/{insight.Id}/settings", InsightSettings(assisted, "Article", "Human", null), Token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, downgrade.StatusCode);
            Assert.Contains("Content.OriginDowngrade", await downgrade.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var quality = await ReadAsync<QualityReportResponse>(await owner.GetAsync($"{Admin}/{insight.Id}/quality", Token));
        Assert.Contains(quality.Findings, finding => finding.Code == "Insight.AiAssisted");
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"{Admin}/{insight.Id}/publish", new ContentVersionRequest(assisted.Version), Token)));

        var published = await PublishAsync(owner, assisted);
        Assert.NotNull(published.ApprovedAtUtc);
        var page = (await ResolveAsync("en", "insights/choosing-a-crm")).Content!;
        Assert.Equal("AiAssisted", page.Insight!.Origin);
        Assert.Null(page.Insight.Author);
        Assert.Contains(AuditAction.ContentApproved, await QueryAsync(db => db.AuditLogs.Where(log => log.EntityId == insight.Id.ToString()).Select(log => log.Action)));
    }

    [Fact]
    public async Task The_worker_publishes_a_scheduled_ai_assisted_insight_only_while_its_approval_stands()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var insight = await CreatePageAsync(owner, "automation-guide", "دليل-الأتمتة", "Insight");
        var assisted = await SettingsAsync(owner, insight, "Guide", "AiAssisted", null);
        var submitted = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{insight.Id}/submit", new ContentVersionRequest(assisted.Version), Token));
        var approved = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{insight.Id}/approve", new ContentVersionRequest(submitted.Version), Token));
        var scheduled = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(
            $"{Admin}/{insight.Id}/schedule", new ScheduleContentRequest(Time.GetUtcNow().AddMinutes(5), "Asia/Riyadh", approved.Version), Token));

        // An edit after scheduling withdraws the approval and the schedule: the Worker has nothing to publish.
        var edited = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{insight.Id}/translations/en", Translation("Automation guide (edited)", "automation-guide", scheduled.Version), Token));
        Assert.Equal("Draft", edited.Status);
        Assert.Null(edited.ScheduledPublishAtUtc);
        Time.Advance(TimeSpan.FromMinutes(6));
        await RunSchedulingJobAsync();
        Assert.Equal("NotFound", (await ResolveAsync("en", "automation-guide")).Kind);
        Assert.Equal("NotFound", (await ResolveAsync("en", "insights/automation-guide")).Kind);

        // Re-approved and re-scheduled, the Worker publishes it when due and announces the URL.
        owner = AuthorizedClient((await SignInAsync("owner@onyx.test", StrongPassword, await AuthenticatorKeyAsync())).AccessToken);
        var resubmitted = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{insight.Id}/submit", new ContentVersionRequest(edited.Version), Token));
        var reapproved = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{insight.Id}/approve", new ContentVersionRequest(resubmitted.Version), Token));
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(
            $"{Admin}/{insight.Id}/schedule", new ScheduleContentRequest(Time.GetUtcNow().AddMinutes(5), null, reapproved.Version), Token));
        Time.Advance(TimeSpan.FromMinutes(6));
        await RunSchedulingJobAsync();

        var page = (await ResolveAsync("en", "insights/automation-guide")).Content!;
        Assert.Equal("Guide", page.Insight!.Kind);
        var announced = await QueryAsync(db => db.OutboxMessages.Where(message => message.Type == typeof(PublicUrlsChanged).FullName).Select(message => message.Payload));
        Assert.Contains(announced, payload => JsonDocument.Parse(payload).RootElement.GetProperty("paths").EnumerateArray().Any(path => path.GetString() == "/en/insights/automation-guide"));
    }

    [Fact]
    public async Task Insights_credit_only_real_consenting_authors_and_fall_back_to_the_organization()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/authors", new SaveAuthorRequest([new LocalizedTextDto("en", "Sample Author")], " ", true, null), Token)));
        var author = await ReadAsync<AuthorResponse>(await owner.PostAsJsonAsync("/api/v1/admin/authors", new SaveAuthorRequest(
            [new LocalizedTextDto("en", "Sample Author", "Sample role"), new LocalizedTextDto("ar", "كاتب تجريبي", null)], "Agreed in writing for tests", true, null), Token));
        var insight = await CreatePageAsync(owner, "sample-insight", "مقال-تجريبي", "Insight");

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync($"{Admin}/{insight.Id}/settings", InsightSettings(insight, "Article", "Human", Guid.NewGuid()), Token)));
        var credited = await SettingsAsync(owner, insight, "Article", "Human", author.Id);
        await PublishAsync(owner, credited);

        var english = (await ResolveAsync("en", "insights/sample-insight")).Content!.Insight!;
        Assert.Equal("Sample Author", english.Author!.Name);
        Assert.Equal("Sample role", english.Author.JobTitle);
        Assert.Equal("كاتب تجريبي", (await ResolveAsync("ar", "المعرفة/مقال-تجريبي")).Content!.Insight!.Author!.Name);
        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.DeleteAsync($"/api/v1/admin/authors/{author.Id}", Token)));

        await ReadAsync<AuthorResponse>(await owner.PutAsJsonAsync($"/api/v1/admin/authors/{author.Id}", new SaveAuthorRequest(
            [new LocalizedTextDto("en", "Sample Author")], "Agreed in writing for tests", false, author.Version), Token));
        Assert.Null((await ResolveAsync("en", "insights/sample-insight")).Content!.Insight!.Author);
    }

    [Fact]
    public async Task Marketing_can_draft_insights_but_not_approve_publish_or_touch_other_types()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("marketing@onyx.test", StrongPassword, SystemRoles.Marketing);
        using var marketing = AuthorizedClient((await SignInAsync("marketing@onyx.test", StrongPassword)).AccessToken);

        var draft = await CreatePageAsync(marketing, "draft-insight", "مسودة-مقال", "Insight");
        var submitted = await ReadAsync<ContentDetailResponse>(await marketing.PostAsJsonAsync($"{Admin}/{draft.Id}/submit", new ContentVersionRequest(draft.Version), Token));

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(marketing.PostAsJsonAsync($"{Admin}/{draft.Id}/approve", new ContentVersionRequest(submitted.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(marketing.PostAsJsonAsync($"{Admin}/{draft.Id}/publish", new ContentVersionRequest(submitted.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(marketing.PostAsJsonAsync(Admin, new CreateContentRequest("Project", null, null, null, 0), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(marketing.PostAsJsonAsync(Admin, new CreateContentRequest("Page", null, null, null, 0), Token)));
    }

    [Fact]
    public async Task Topic_pages_list_their_insights_newest_first()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var topic = await PublishAsync(owner, await CreatePageAsync(owner, "automation", "الأتمتة", "Topic"));
        var older = await PublishAsync(owner, await CreatePageAsync(owner, "older-insight", "مقال-أقدم", "Insight"));
        Time.Advance(TimeSpan.FromMinutes(2));
        var newer = await PublishAsync(owner, await CreatePageAsync(owner, "newer-insight", "مقال-أحدث", "Insight"));
        await PublishAsync(owner, await CreatePageAsync(owner, "untagged-insight", "مقال-آخر", "Insight"));
        foreach (var tagged in new[] { older, newer })
        {
            await ReadAsync<ContentRelationResponse>(await owner.PostAsJsonAsync($"{Admin}/{tagged.Id}/relations", new AddContentRelationRequest(topic.Id, "Related", 0, null), Token));
        }

        Assert.Equal("Content", (await ResolveAsync("en", "insights/topics/automation")).Kind);
        Assert.Equal("Content", (await ResolveAsync("ar", "المعرفة/مواضيع/الأتمتة")).Kind);
        var byTopic = await ReadAsync<PagedResult<ListingItemResponse>>(await Client.GetAsync($"/api/v1/content/listing?language=en&section=insights&topic={topic.Id}", Token));
        var all = await ReadAsync<PagedResult<ListingItemResponse>>(await Client.GetAsync("/api/v1/content/listing?language=en&section=insights", Token));

        Assert.Equal(["/en/insights/newer-insight", "/en/insights/older-insight"], byTopic.Items.Select(item => item.Path));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.GetAsync($"/api/v1/content/listing?language=en&section=glossary&topic={topic.Id}", Token)));
    }

    [Fact]
    public async Task Glossary_terms_need_no_proof_but_integrations_are_verified_showcase_items()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await PublishAsync(owner, await CreatePageAsync(owner, "api", "واجهة-برمجية", "GlossaryTerm"));
        Assert.Equal("Content", (await ResolveAsync("en", "glossary/api")).Kind);

        var integration = await CreatePageAsync(owner, "sample-integration", "تكامل-تجريبي", "Integration");
        var submitted = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{integration.Id}/submit", new ContentVersionRequest(integration.Version), Token));
        using var refused = await owner.PostAsJsonAsync($"{Admin}/{integration.Id}/approve", new ContentVersionRequest(submitted.Version), Token);
        Assert.Contains("Proof.NotVerified", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        var verified = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(
            $"{Admin}/{integration.Id}/proof-verification", new VerifyProofRequest("Delivered for a client project", "NotApplicable", null, submitted.Version), Token));
        var approved = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{integration.Id}/approve", new ContentVersionRequest(verified.Version), Token));
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{integration.Id}/publish", new ContentVersionRequest(approved.Version), Token));

        Assert.Equal("Content", (await ResolveAsync("ar", "التكاملات/تكامل-تجريبي")).Kind);
        Assert.Single((await ReadAsync<PagedResult<ListingItemResponse>>(await Client.GetAsync("/api/v1/content/listing?language=en&section=integrations", Token))).Items);
    }

    [Fact]
    public async Task Article_structured_data_is_served_for_insights_only()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var article = new SeoProfileDto(null, null, null, null, null, null, true, true, ["WebPage", "Article"], null, null, null, null, null, null);
        var insight = await CreatePageAsync(owner, "sample-insight", "مقال-تجريبي", "Insight");
        await PublishAsync(owner, await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync(
            $"{Admin}/{insight.Id}/translations/en/seo", new UpdateContentSeoRequest(article, insight.Version), Token)));
        Assert.Contains("Article", (await ResolveAsync("en", "insights/sample-insight")).Content!.Seo.StructuredData);

        var page = await CreatePageAsync(owner, "about-us", "صفحة-تعريفية");
        await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(article, page.Version), Token));
        var quality = await ReadAsync<QualityReportResponse>(await owner.GetAsync($"{Admin}/{page.Id}/quality", Token));
        Assert.Contains(quality.Findings, finding => finding.Code == "Schema.ArticleOnNonInsight" && finding.Severity == "Error");
    }

    [Fact]
    public async Task Restoring_an_earlier_human_revision_keeps_the_ai_assisted_label()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var insight = await CreatePageAsync(owner, "sample-insight", "مقال-تجريبي", "Insight");
        var assisted = await SettingsAsync(owner, insight, "Article", "AiAssisted", null);

        var restored = await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync(
            $"{Admin}/{insight.Id}/revisions/{insight.CurrentRevisionNumber}/restore", new RestoreRevisionRequest(assisted.Version, null), Token));

        Assert.Equal("AiAssisted", restored.Origin);
    }

    private static UpdateContentSettingsRequest InsightSettings(ContentDetailResponse content, string kind, string origin, Guid? authorId) =>
        new(content.ParentId, content.Key, content.Presentation, content.SortOrder, content.PrimaryCtaId, content.FeaturedImageId, content.Version, kind, origin, authorId);

    private static async Task<ContentDetailResponse> SettingsAsync(HttpClient client, ContentDetailResponse content, string kind, string origin, Guid? authorId) =>
        await ReadAsync<ContentDetailResponse>(await client.PutAsJsonAsync($"{Admin}/{content.Id}/settings", InsightSettings(content, kind, origin, authorId), Token));

    private async Task<string> AuthenticatorKeyAsync()
    {
        var userId = (await QueryAsync(db => db.Users.Where(user => user.Email == "owner@onyx.test").Select(user => user.Id))).Single();
        return await EnableTwoFactorAsync(userId);
    }

    private async Task RunSchedulingJobAsync()
    {
        await using var worker = WorkerTestHost.Create(Fixture.ApplicationDatabase!.ConnectionString, "worker-a", Time);
        await worker.InScopeAsync(async services =>
        {
            var job = services.GetServices<Onyx.Application.Jobs.IScheduledJob>().Single(candidate => candidate.Name == ContentSchedulingJob.JobName);
            return await job.ExecuteAsync(Token);
        });
    }
}
