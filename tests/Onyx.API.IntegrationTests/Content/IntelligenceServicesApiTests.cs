using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.API.IntegrationTests.Content;

/// <summary>Keyword map, internal link analysis, SEO recommendations, publishing-time advice and competitor references.</summary>
public sealed class IntelligenceServicesApiTests(PostgresFixture fixture) : ContentApiTestBase(fixture)
{
    [Fact]
    public async Task Keyword_conflicts_cover_content_and_the_keyword_map_ignoring_case_and_the_page_itself()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var seo = new SeoProfileDto("Cloud Migration", null, "Commercial", null, null, null, true, true, ["WebPage"], null, null, null, null, null, null);
        await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{page.Id}/translations/en/seo", new UpdateContentSeoRequest(seo, page.Version), Token));
        await ReadAsync<KeywordTargetResponse>(await owner.PostAsJsonAsync(
            "/api/v1/admin/keywords", new SaveKeywordTargetRequest("data platforms", "en", null, "Informational", null, null, "Planned", null, null), Token));

        var content = await ReadAsync<KeywordConflictResponse>(await owner.GetAsync("/api/v1/admin/keywords/conflicts?language=en&keyword=cloud%20MIGRATION", Token));
        var mapped = await ReadAsync<KeywordConflictResponse>(await owner.GetAsync("/api/v1/admin/keywords/conflicts?language=en&keyword=Data%20Platforms", Token));
        var self = await ReadAsync<KeywordConflictResponse>(await owner.GetAsync($"/api/v1/admin/keywords/conflicts?language=en&keyword=cloud%20migration&excludeContentItemId={page.Id}", Token));
        var otherLanguage = await ReadAsync<KeywordConflictResponse>(await owner.GetAsync("/api/v1/admin/keywords/conflicts?language=ar&keyword=cloud%20migration", Token));

        Assert.True(content.HasConflict);
        Assert.Contains(content.Conflicts, conflict => conflict.ContentItemId == page.Id);
        Assert.True(mapped.HasConflict);
        Assert.False(self.HasConflict);
        Assert.False(otherLanguage.HasConflict);
        var listed = await ReadAsync<PagedResult<KeywordTargetResponse>>(await owner.GetAsync("/api/v1/admin/keywords", Token));
        Assert.All(listed.Items, keyword => Assert.NotEqual("Available", keyword.PerformanceDataStatus));
    }

    [Fact]
    public async Task The_link_report_finds_broken_links_orphans_and_ambiguous_anchors_and_proposals_are_not_duplicated()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var target = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "target", arSlug: "هدف"));
        var other = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "other", arSlug: "اخر"));
        var third = await PublishAsync(owner, await CreatePageAsync(owner, enSlug: "third", arSlug: "ثالث"));
        var source = await CreatePageAsync(owner, enSlug: "source", arSlug: "مصدر");
        var withLinks = await ReadAsync<ContentDetailResponse>(await owner.PutAsJsonAsync($"{Admin}/{source.Id}/translations/en", new UpdateContentTranslationRequest(
            "Source", "Source summary.", "source",
            [new ContentBlockDto("Paragraph", Spans:
            [
                new TextSpanDto("old page", Link: new LinkTargetDto(ContentItemId: target.Id)),
                new TextSpanDto(", "),
                new TextSpanDto("learn more", Link: new LinkTargetDto(ContentItemId: other.Id)),
                new TextSpanDto(" and "),
                new TextSpanDto("learn more", Link: new LinkTargetDto(ContentItemId: third.Id)),
            ])], [], source.Version), Token));
        await PublishAsync(owner, withLinks);
        await ReadAsync<ContentDetailResponse>(await owner.PostAsJsonAsync($"{Admin}/{target.Id}/unpublish", new TakeOfflineRequest("Gone", null, target.Version), Token));

        var report = await ReadAsync<InternalLinkReportResponse>(await owner.GetAsync("/api/v1/admin/intelligence/link-report", Token));

        var broken = Assert.Single(report.BrokenLinks);
        Assert.Equal(source.Id, broken.ContentItemId);
        Assert.Equal("Target is not live", broken.Problem);
        Assert.Contains(report.Orphans, orphan => orphan.ContentItemId == source.Id && orphan.LanguageCode == "en");
        Assert.DoesNotContain(report.Orphans, orphan => orphan.ContentItemId == other.Id && orphan.LanguageCode == "en");
        Assert.Contains(report.RepeatedAnchors, anchor => anchor.AnchorText == "learn more" && anchor.DistinctTargets == 2);

        var first = await ReadAsync<GeneratedRecommendationsResponse>(await owner.PostAsync("/api/v1/admin/intelligence/link-report/recommendations", null, Token));
        var second = await ReadAsync<GeneratedRecommendationsResponse>(await owner.PostAsync("/api/v1/admin/intelligence/link-report/recommendations", null, Token));
        Assert.True(first.Created > 0);
        Assert.Equal(0, second.Created);
        Assert.Equal(first.Created, second.SkippedExisting);
    }

    [Fact]
    public async Task Applying_an_approved_recommendation_creates_a_draft_revision_and_never_publishes()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        using var seoEditor = await SignInWithAsync("seo-editor", Permissions.Content.View, Permissions.Seo.Edit, Permissions.Pages.View, Permissions.Pages.Edit);
        var page = await PublishAsync(owner, await CreatePageAsync(owner));
        var proposal = await ReadAsync<RecommendationResponse>(await seoEditor.PostAsJsonAsync("/api/v1/admin/seo-recommendations",
            new ProposeRecommendationRequest("MetaTitle", "Clearer meta title", "The title does not describe the page.", null,
                [new ProposedChangeDto("metaTitle", null, "About Onyx Vanguard Solutions")], page.Id, "en", null), Token));

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(seoEditor.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/apply", new DecideRequest(null, proposal.Version), Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(seoEditor.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/approve", new DecideRequest(null, proposal.Version), Token)));

        var approved = await ReadAsync<RecommendationResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/approve", new DecideRequest("ok", proposal.Version), Token));
        var applied = await ReadAsync<RecommendationResponse>(await seoEditor.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/apply", new DecideRequest(null, approved.Version), Token));

        Assert.Equal("Applied", applied.Status);
        var detail = await ReadAsync<ContentDetailResponse>(await owner.GetAsync($"{Admin}/{page.Id}", Token));
        Assert.Equal("About Onyx Vanguard Solutions", detail.Translations.Single(t => t.LanguageCode == "en").Seo.MetaTitle);
        Assert.Equal("Draft", detail.Status);
        Assert.True(detail.HasUnpublishedChanges);
        Assert.True(detail.CurrentRevisionNumber > page.CurrentRevisionNumber);
        Assert.Equal("Sample page", (await ResolveAsync("en", "about-us")).Content!.Seo.Title);
    }

    [Fact]
    public async Task Changes_that_cannot_be_applied_automatically_must_be_marked_applied_by_hand()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);
        var proposal = await ReadAsync<RecommendationResponse>(await owner.PostAsJsonAsync("/api/v1/admin/seo-recommendations",
            new ProposeRecommendationRequest("HeadingStructure", "Add a section", "The page has no sections.", null, [new ProposedChangeDto("body", null, "## Approach")], page.Id, "en", null), Token));
        var approved = await ReadAsync<RecommendationResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/approve", new DecideRequest(null, proposal.Version), Token));

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/apply", new DecideRequest(null, approved.Version), Token)));
        var marked = await ReadAsync<RecommendationResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/mark-applied", new DecideRequest("Done by hand", approved.Version), Token));
        Assert.Equal("Applied", marked.Status);
        var rejectedLater = await StatusAsync(owner.PostAsJsonAsync($"/api/v1/admin/seo-recommendations/{proposal.Id}/reject", new DecideRequest(null, marked.Version), Token));
        Assert.Equal(HttpStatusCode.Conflict, rejectedLater);
    }

    [Fact]
    public async Task Publishing_time_advice_is_audience_timing_and_reports_insufficient_data()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        var page = await CreatePageAsync(owner);

        var advice = await ReadAsync<PublishingTimeRecommendationResponse>(await owner.GetAsync($"{Admin}/{page.Id}/publishing-time", Token));

        Assert.Equal("InsufficientData", advice.Basis);
        Assert.Null(advice.RecommendedAtUtc);
        Assert.Contains("not a search ranking factor", advice.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Competitor_references_are_validated_and_normalized()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/competitors", new SaveCompetitorRequest("Sample", "https://example.org/path", [], [], [], null, true, null), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/competitors", new SaveCompetitorRequest("", "example.org", [], [], [], null, true, null), Token)));
        var created = await ReadAsync<CompetitorResponse>(await owner.PostAsJsonAsync(
            "/api/v1/admin/competitors", new SaveCompetitorRequest("Sample", " Example.ORG ", ["SA"], ["ar"], ["cloud"], null, true, null), Token));

        Assert.Equal("example.org", created.Domain);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.GetAsync("/api/v1/competitors", Token)));
    }
}
