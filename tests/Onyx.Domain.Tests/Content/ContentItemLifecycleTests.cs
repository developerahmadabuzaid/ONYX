using Onyx.Domain.Content;

namespace Onyx.Domain.Tests.Content;

public sealed class ContentItemLifecycleTests
{
    private static readonly string[] Languages = ["ar", "en"];
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Content_must_be_submitted_approved_and_then_published()
    {
        var item = CompletePage();

        Assert.True(item.Publish(Now, Languages).IsFailure);
        Assert.True(item.Approve(Guid.NewGuid(), Now, Languages).IsFailure);

        Assert.True(item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(ContentStatus.PendingReview, item.Status);
        Assert.True(item.Publish(Now, Languages).IsFailure);

        Assert.True(item.Approve(Guid.NewGuid(), Now, Languages).IsSuccess);
        Assert.Equal(ContentStatus.Approved, item.Status);
        Assert.False(item.IsLive);

        Assert.True(item.Publish(Now, Languages).IsSuccess);
        Assert.Equal(ContentStatus.Published, item.Status);
        Assert.True(item.IsLive);
        Assert.Equal(Now, item.FirstPublishedAtUtc);
    }

    [Fact]
    public void Editing_reviewed_content_returns_it_to_draft_but_keeps_the_live_revision()
    {
        var item = PublishedPage();
        var live = item.LiveRevisionId;

        Assert.True(item.UpdateTranslation("en", "Changed", "Summary", "changed", [], []).IsSuccess);

        Assert.Equal(ContentStatus.Draft, item.Status);
        Assert.Equal(live, item.LiveRevisionId);
        Assert.True(item.IsLive);
    }

    [Fact]
    public void Approval_requires_every_language_to_be_complete()
    {
        var item = ContentItem.Create(ContentType.Page, null, null, ContentPresentation.Page, 0).Value;
        Assert.True(item.UpdateTranslation("ar", "عنوان", "ملخص", "عنوان", [], []).IsSuccess);
        Assert.True(item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now).IsSuccess);

        var approved = item.Approve(Guid.NewGuid(), Now, Languages);

        Assert.True(approved.IsFailure);
        Assert.Equal(ContentErrors.IncompleteTranslations.Code, approved.Error!.Code);
    }

    [Fact]
    public void Rejection_needs_a_note_and_returns_to_draft()
    {
        var item = CompletePage();
        item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.True(item.Reject("  ").IsFailure);
        Assert.True(item.Reject("Please add the approach section.").IsSuccess);
        Assert.Equal(ContentStatus.Draft, item.Status);
        Assert.Equal("Please add the approach section.", item.ReviewNote);
    }

    [Fact]
    public void Scheduling_needs_approval_and_a_future_time_and_publishes_only_when_due()
    {
        var item = CompletePage();
        Assert.True(item.Schedule(Now.AddHours(1), "Asia/Riyadh", Now).IsFailure);
        item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        item.Approve(Guid.NewGuid(), Now, Languages);

        Assert.True(item.Schedule(Now.AddMinutes(-1), null, Now).IsFailure);
        Assert.True(item.Schedule(Now.AddHours(1), "Asia/Riyadh", Now).IsSuccess);
        Assert.Equal(ContentStatus.Scheduled, item.Status);

        Assert.True(item.PublishScheduled(Now.AddMinutes(30), Languages).IsFailure);
        Assert.True(item.PublishScheduled(Now.AddHours(1), Languages).IsSuccess);
        Assert.True(item.IsLive);
    }

    [Fact]
    public void Unpublishing_without_a_decision_flags_the_urls_and_never_redirects_to_the_homepage()
    {
        var item = PublishedPage();

        Assert.True(item.Unpublish(OfflineResolution.None, null).IsSuccess);

        Assert.False(item.IsLive);
        Assert.Equal(OfflineResolution.PendingDecision, item.OfflineResolution);
        Assert.Null(item.OfflineRedirectToId);
    }

    [Fact]
    public void A_redirect_resolution_needs_another_target()
    {
        var item = PublishedPage();

        Assert.True(item.Unpublish(OfflineResolution.RedirectToContent, null).IsFailure);
        Assert.True(item.Unpublish(OfflineResolution.RedirectToContent, item.Id).IsFailure);
        Assert.True(item.Unpublish(OfflineResolution.Gone, Guid.NewGuid()).IsFailure);

        var target = Guid.NewGuid();
        Assert.True(item.Unpublish(OfflineResolution.RedirectToContent, target).IsSuccess);
        Assert.Equal(target, item.OfflineRedirectToId);
    }

    [Fact]
    public void Archived_content_is_offline_and_cannot_be_edited_until_restored()
    {
        var item = PublishedPage();

        Assert.True(item.Archive(OfflineResolution.Gone, null, Now).IsSuccess);
        Assert.False(item.IsLive);
        Assert.Equal(ContentStatus.Archived, item.Status);
        Assert.True(item.UpdateTranslation("en", "x", "y", "x", [], []).IsFailure);

        Assert.True(item.RestoreFromArchive().IsSuccess);
        Assert.Equal(ContentStatus.Draft, item.Status);
        Assert.False(item.IsLive);
    }

    [Fact]
    public void Scheduled_unpublishing_needs_live_content_and_a_future_time()
    {
        var draft = CompletePage();
        Assert.True(draft.ScheduleUnpublish(Now.AddDays(1), Now).IsFailure);

        var item = PublishedPage();
        Assert.True(item.ScheduleUnpublish(Now.AddMinutes(-5), Now).IsFailure);
        Assert.True(item.ScheduleUnpublish(Now.AddDays(1), Now).IsSuccess);
        Assert.True(item.UnpublishScheduled(Now).IsFailure);
        Assert.True(item.UnpublishScheduled(Now.AddDays(1)).IsSuccess);
        Assert.Equal(OfflineResolution.PendingDecision, item.OfflineResolution);
    }

    [Theory]
    [InlineData(ContentType.ServicePillar, true)]
    [InlineData(ContentType.Industry, true)]
    [InlineData(ContentType.Service, false)]
    public void Parent_shape_is_enforced(ContentType type, bool withParentFails)
    {
        var withParent = ContentItem.Create(type, Guid.NewGuid(), null, ContentPresentation.Page, 0);
        var withoutParent = ContentItem.Create(type, null, null, ContentPresentation.Page, 0);

        Assert.Equal(withParentFails, withParent.IsFailure);
        Assert.Equal(!withParentFails, withoutParent.IsFailure);
    }

    [Fact]
    public void Only_industries_can_be_taxonomy_only_and_they_cannot_be_published()
    {
        Assert.True(ContentItem.Create(ContentType.Page, null, null, ContentPresentation.TaxonomyOnly, 0).IsFailure);

        var tag = ContentItem.Create(ContentType.Industry, null, null, ContentPresentation.TaxonomyOnly, 0).Value;
        Assert.False(tag.HasPage);
    }

    [Fact]
    public void An_industry_page_needs_a_summary_and_three_sections_in_every_language()
    {
        var item = ContentItem.Create(ContentType.Industry, null, null, ContentPresentation.Page, 0).Value;
        item.UpdateTranslation("ar", "الرعاية الصحية", "ملخص", "الرعاية-الصحية", Sections(2), []);
        item.UpdateTranslation("en", "Healthcare", "Summary", "healthcare", Sections(3), []);

        Assert.Equal(ContentErrors.IndustryMinimum.Code, item.CheckPublishable(Languages)!.Code);

        item.UpdateTranslation("ar", "الرعاية الصحية", "ملخص", "الرعاية-الصحية", Sections(3), []);
        Assert.Null(item.CheckPublishable(Languages));
    }

    [Fact]
    public void Keys_are_restricted_to_safe_characters()
    {
        Assert.True(ContentItem.Create(ContentType.Page, null, "about", ContentPresentation.Page, 0).IsSuccess);
        Assert.True(ContentItem.Create(ContentType.Page, null, "About Us", ContentPresentation.Page, 0).IsFailure);
        Assert.True(ContentItem.Create(ContentType.Page, null, "../x", ContentPresentation.Page, 0).IsFailure);
    }

    internal static ContentItem CompletePage()
    {
        var item = ContentItem.Create(ContentType.Page, null, null, ContentPresentation.Page, 0).Value;
        Assert.True(item.UpdateTranslation("ar", "من نحن", "ملخص الصفحة", "من-نحن", [], []).IsSuccess);
        Assert.True(item.UpdateTranslation("en", "About", "Page summary", "about", [], []).IsSuccess);
        return item;
    }

    private static ContentItem PublishedPage()
    {
        var item = CompletePage();
        item.SubmitForReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        item.Approve(Guid.NewGuid(), Now, Languages);
        Assert.True(item.Publish(Now, Languages).IsSuccess);
        return item;
    }

    private static List<ContentBlock> Sections(int count)
    {
        var blocks = new List<ContentBlock>();
        for (var index = 0; index < count; index++)
        {
            blocks.Add(new ContentBlock(ContentBlockType.Heading, Level: 2, Spans: [new TextSpan($"Section {index}")]));
            blocks.Add(new ContentBlock(ContentBlockType.Paragraph, Spans: [new TextSpan("Real text.")]));
        }

        return blocks;
    }
}
