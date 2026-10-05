using Onyx.Domain.Content;
using Onyx.Domain.Insights;
using Onyx.Domain.Proof;

namespace Onyx.Domain.Tests.Content;

public sealed class WorkProofInsightRulesTests
{
    private static readonly string[] Languages = ["ar", "en"];
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ContentType.Project)]
    [InlineData(ContentType.CaseStudy)]
    [InlineData(ContentType.Integration)]
    public void Proof_content_cannot_be_published_or_scheduled_without_a_verification(ContentType type)
    {
        var item = Approved(type);

        Assert.Equal(ContentErrors.ProofNotVerified.Code, item.Publish(Now, Languages).Error!.Code);
        Assert.Equal(ContentErrors.ProofNotVerified.Code, item.Schedule(Now.AddHours(1), null, Now).Error!.Code);

        Assert.True(item.VerifyProof(Guid.NewGuid(), Now, "Owner confirmation", ClientPermission.NotApplicable, null).IsSuccess);
        Assert.True(item.Publish(Now, Languages).IsSuccess);
    }

    [Fact]
    public void Any_edit_clears_the_verification_and_live_proof_cannot_be_unverified()
    {
        var item = Approved(ContentType.Project);
        item.VerifyProof(Guid.NewGuid(), Now, "Owner confirmation", ClientPermission.NotApplicable, null);
        item.Publish(Now, Languages);

        Assert.True(item.RevokeProofVerification().IsFailure);

        item.UpdateTranslation("en", "Changed", "Summary", "project", [], []);
        Assert.Null(item.ProofVerification);
    }

    [Fact]
    public void Verification_needs_a_source_and_a_recorded_permission_when_granted()
    {
        var item = Draft(ContentType.Project);

        Assert.True(item.VerifyProof(Guid.NewGuid(), Now, " ", ClientPermission.NotApplicable, null).IsFailure);
        Assert.True(item.VerifyProof(Guid.NewGuid(), Now, "Contract", ClientPermission.Granted, null).IsFailure);
        Assert.True(item.VerifyProof(Guid.NewGuid(), Now, "Contract", ClientPermission.Granted, "Signed release, 2026-09-30").IsSuccess);
        Assert.True(Draft(ContentType.Page).VerifyProof(Guid.NewGuid(), Now, "Contract", ClientPermission.NotApplicable, null).IsFailure);
    }

    [Fact]
    public void Non_proof_content_publishes_without_verification() =>
        Assert.True(Approved(ContentType.GlossaryTerm).Publish(Now, Languages).IsSuccess);

    [Fact]
    public void Ai_assisted_insights_cannot_be_relabelled_as_human_written()
    {
        var insight = Draft(ContentType.Insight);
        Assert.Equal(InsightKind.Article, insight.InsightKind);

        Assert.True(insight.UpdateInsightSettings(InsightKind.Guide, InsightOrigin.AiAssisted, null).IsSuccess);
        Assert.Equal("Content.OriginDowngrade", insight.UpdateInsightSettings(InsightKind.Guide, InsightOrigin.Human, null).Error!.Code);
        Assert.True(Draft(ContentType.Page).UpdateInsightSettings(InsightKind.Article, InsightOrigin.Human, null).IsFailure);
    }

    [Fact]
    public void An_ai_assisted_insight_publishes_only_from_a_recorded_approval_and_edits_withdraw_it()
    {
        var insight = Draft(ContentType.Insight);
        insight.UpdateInsightSettings(InsightKind.Article, InsightOrigin.AiAssisted, null);
        Assert.True(insight.Publish(Now, Languages).IsFailure);

        insight.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        var reviewer = Guid.NewGuid();
        insight.Approve(reviewer, Now, Languages);
        Assert.True(insight.Schedule(Now.AddHours(1), null, Now).IsSuccess);

        insight.UpdateTranslation("en", "Edited after approval", "Summary", "insight", [], []);
        Assert.Equal(ContentStatus.Draft, insight.Status);
        Assert.Null(insight.ApprovedById);
        Assert.Null(insight.ScheduledPublishAtUtc);
        Assert.True(insight.PublishScheduled(Now.AddHours(2), Languages).IsFailure);
    }

    [Fact]
    public void Case_studies_belong_to_a_project_and_share_one_slug_section()
    {
        Assert.True(ContentItem.Create(ContentType.CaseStudy, null, null, ContentPresentation.Page, 0).IsFailure);
        Assert.True(ContentItem.Create(ContentType.Project, Guid.NewGuid(), null, ContentPresentation.Page, 0).IsFailure);
        Assert.Equal(ContentItem.BuildSlugScope(ContentType.CaseStudy, Guid.NewGuid()), ContentItem.BuildSlugScope(ContentType.CaseStudy, Guid.NewGuid()));
        Assert.NotEqual(ContentItem.BuildSlugScope(ContentType.Page, Guid.NewGuid()), ContentItem.BuildSlugScope(ContentType.Page, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void Featured_slots_are_one_to_three(int slot, bool valid) =>
        Assert.Equal(valid, FeaturedProject.Create(Guid.NewGuid(), slot, Guid.NewGuid(), Now).IsSuccess);

    [Fact]
    public void Proof_items_need_their_evidence_and_publish_only_once_verified()
    {
        var project = Guid.NewGuid();
        Assert.True(ProofItem.Create(project, ProofItemKind.Metric, [new LocalizedText("en", "Faster reports")], "35%", null, null, null, null, 0).IsFailure);
        Assert.True(ProofItem.Create(project, ProofItemKind.Capability, [new LocalizedText("en", "Booking")], "35%", null, null, null, null, 0).IsFailure);
        Assert.True(ProofItem.Create(project, ProofItemKind.Testimonial, [new LocalizedText("en", "Great")], null, null, null, null, null, 0).IsFailure);
        Assert.True(ProofItem.Create(project, ProofItemKind.Capability, [], null, null, null, null, null, 0).IsFailure);

        var metric = ProofItem.Create(project, ProofItemKind.Metric, [new LocalizedText("en", "Faster reports")], "35%", new DateOnly(2026, 9, 1), null, null, null, 0).Value;
        Assert.True(metric.Publish(Now).IsFailure);
        metric.Verify(Guid.NewGuid(), Now, "Client report, September 2026", ClientPermission.NotApplicable, null);
        Assert.True(metric.Publish(Now).IsSuccess);

        metric.Update([new LocalizedText("en", "Faster monthly reports")], "35%", new DateOnly(2026, 9, 1), null, null, null, 0);
        Assert.Equal(ProofItemStatus.Draft, metric.Status);
        Assert.Null(metric.Verification);
    }

    [Fact]
    public void Testimonials_need_the_clients_recorded_permission()
    {
        var testimonial = ProofItem.Create(Guid.NewGuid(), ProofItemKind.Testimonial, [new LocalizedText("en", "Clear and reliable.")], null, null, "Sample Person", null, null, 0).Value;

        Assert.Equal("Proof.ClientPermissionRequired", testimonial.Verify(Guid.NewGuid(), Now, "Email", ClientPermission.NotApplicable, null).Error!.Code);
        Assert.True(testimonial.Verify(Guid.NewGuid(), Now, "Email", ClientPermission.Granted, "Written approval").IsSuccess);
    }

    [Fact]
    public void Authors_are_named_people_with_recorded_consent()
    {
        Assert.True(Author.Create([new LocalizedText("en", "Sample Author")], null).IsFailure);
        Assert.True(Author.Create([], "Consent").IsFailure);
        Assert.True(Author.Create([new LocalizedText("en", "A"), new LocalizedText("en", "B")], "Consent").IsFailure);
        Assert.True(Author.Create([new LocalizedText("en", "Sample Author", "Engineer")], "Agreed in writing").IsSuccess);
    }

    private static ContentItem Draft(ContentType type)
    {
        var parent = type == ContentType.CaseStudy ? Guid.NewGuid() : (Guid?)null;
        var item = ContentItem.Create(type, parent, null, ContentPresentation.Page, 0).Value;
        item.UpdateTranslation("ar", "عنوان", "ملخص", "عنوان", [], []);
        item.UpdateTranslation("en", "Title", "Summary", "title", [], []);
        return item;
    }

    private static ContentItem Approved(ContentType type)
    {
        var item = Draft(type);
        item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True(item.Approve(Guid.NewGuid(), Now, Languages).IsSuccess);
        return item;
    }
}
