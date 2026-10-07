using Onyx.Domain.News;

namespace Onyx.Domain.Tests.News;

/// <summary>The News Hub guards: no summary means no publication, and automatic publication only under every condition.</summary>
public sealed class NewsItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly string[] Languages = ["ar", "en"];

    private static readonly NewsText Arabic = new("ar", "عنوان تجريبي", "ملخص تجريبي مكتوب بصياغة Onyx الخاصة لأغراض الاختبار فقط، وهو أطول من الحد الأدنى.");
    private static readonly NewsText English = new("en", "Sample headline", "A sample summary written in Onyx's own words for tests only, longer than the minimum.");

    private static readonly Dictionary<string, string> Slugs = new(StringComparer.Ordinal) { ["ar"] = "عنوان-تجريبي", ["en"] = "sample-headline" };

    [Fact]
    public void An_ingested_item_is_held_without_a_summary_and_keeps_only_metadata()
    {
        var item = Ingest();

        Assert.Equal(NewsItemStatus.Held, item.Status);
        Assert.Equal(NewsHoldReason.SummaryUnavailable, item.HoldReason);
        Assert.False(item.IsPublic);
        Assert.Empty(item.Translations);
        Assert.Equal("Sample Publisher", item.Publisher);
        Assert.Equal(NewsUrl.Hash("https://publisher.example.com/story"), item.OriginalUrlHash);
    }

    [Fact]
    public void A_source_date_far_in_the_future_is_not_trusted_and_a_missing_date_stays_missing()
    {
        Assert.Null(NewsItem.Ingest(Source(), "https://publisher.example.com/a", "Title", Now.AddDays(3), Guid.CreateVersion7(), "title", null, Now).Value.SourcePublishedAtUtc);
        Assert.Null(NewsItem.Ingest(Source(), "https://publisher.example.com/b", "Title", null, Guid.CreateVersion7(), "title", null, Now).Value.SourcePublishedAtUtc);
        Assert.Equal(Now.AddHours(-2), NewsItem.Ingest(Source(), "https://publisher.example.com/c", "Title", Now.AddHours(-2), Guid.CreateVersion7(), "title", null, Now).Value.SourcePublishedAtUtc);
    }

    [Fact]
    public void No_summary_in_every_language_means_no_publication()
    {
        var item = Ingest();
        Assert.Equal("News.SummaryRequired", item.Publish(Now, Languages).Error!.Code);

        item.EditTranslation("en", English.Title, English.Summary, "sample-headline");
        Assert.Equal("News.SummaryRequired", item.Publish(Now, Languages).Error!.Code);

        item.EditTranslation("ar", Arabic.Title, Arabic.Summary, "عنوان-تجريبي");
        Assert.True(item.Publish(Now, Languages).IsSuccess);
        Assert.True(item.IsPublic);
        Assert.False(item.AutoPublished);
        Assert.Equal(Now, item.PublishedAtUtc);
        Assert.Contains(item.DomainEvents, domainEvent => domainEvent is NewsItemPublicationChanged);
    }

    [Theory]
    [InlineData("<script>alert(1)</script> and enough words to pass the length rule")]
    [InlineData("too short")]
    public void Summaries_are_plain_text_within_the_length_limits(string summary) =>
        Assert.True(Ingest().EditTranslation("en", "Headline", summary, "headline").IsFailure);

    [Fact]
    public void Automatic_publication_needs_generated_summaries_and_an_allowlisted_auto_publishing_source()
    {
        var manualSource = Source(autoPublish: false);
        var item = Ingest(manualSource);
        item.ApplyGeneratedSummaries([Arabic, English], "ai.news-summary", Slugs);
        Assert.Equal("News.SourceNotAutoPublishing", item.AutoPublish(Now, manualSource, Languages).Error!.Code);

        var autoSource = Source(autoPublish: true);
        var generated = Ingest(autoSource);
        Assert.Equal("News.SummaryRequired", generated.AutoPublish(Now, autoSource, Languages).Error!.Code);
        generated.ApplyGeneratedSummaries([Arabic, English], "ai.news-summary", Slugs);
        Assert.True(generated.AutoPublish(Now, autoSource, Languages).IsSuccess);
        Assert.True(generated.AutoPublished);

        autoSource.Block();
        var afterBlock = Ingest(autoSource);
        afterBlock.ApplyGeneratedSummaries([Arabic, English], "ai.news-summary", Slugs);
        Assert.True(afterBlock.AutoPublish(Now, autoSource, Languages).IsFailure);
    }

    [Fact]
    public void Editor_texts_are_never_overwritten_and_edited_items_never_auto_publish()
    {
        var source = Source(autoPublish: true);
        var item = Ingest(source);
        item.EditTranslation("ar", Arabic.Title, Arabic.Summary, "عنوان-تجريبي");
        item.EditTranslation("en", English.Title, English.Summary, "sample-headline");

        Assert.Equal("News.NotGeneratable", item.ApplyGeneratedSummaries([Arabic, English], "ai.news-summary", Slugs).Error!.Code);
        Assert.Equal("News.SummaryRequired", item.AutoPublish(Now, source, Languages).Error!.Code);
    }

    [Fact]
    public void A_similar_recent_item_is_held_for_an_editor_even_with_generated_summaries()
    {
        var source = Source(autoPublish: true);
        var item = NewsItem.Ingest(source, "https://publisher.example.com/dup", "Same story", Now, Guid.CreateVersion7(), "same story", Guid.CreateVersion7(), Now).Value;
        item.ApplyGeneratedSummaries([Arabic, English], "ai.news-summary", Slugs);

        Assert.Equal(NewsHoldReason.SimilarItem, item.HoldReason);
        Assert.True(item.AutoPublish(Now, source, Languages).IsFailure);
    }

    [Fact]
    public void Slugs_are_fixed_once_the_item_has_been_public()
    {
        var item = Published();
        item.EditTranslation("en", "A new headline", English.Summary, "a-new-headline");

        Assert.Equal("sample-headline", item.Translation("en")!.Slug);
        Assert.Equal("A new headline", item.Translation("en")!.Title);
    }

    [Fact]
    public void Taking_an_item_down_and_deleting_it_follow_the_lifecycle()
    {
        var item = Published();
        Assert.True(item.Unpublish().IsSuccess);
        Assert.False(item.IsPublic);
        Assert.True(item.Publish(Now, Languages).IsSuccess);
        Assert.True(item.Hide().IsSuccess);
        Assert.Equal("News.InvalidTransition", Ingest().Unpublish().Error!.Code);

        Assert.True(item.Delete().IsSuccess);
        Assert.Equal(NewsItemStatus.Deleted, item.Status);
        Assert.Empty(item.Translations);
        Assert.Equal("News.NotPublishable", item.Publish(Now, Languages).Error!.Code);
        Assert.Equal("News.Deleted", item.EditTranslation("en", English.Title, English.Summary, "x").Error!.Code);
    }

    private static NewsItem Published()
    {
        var item = Ingest();
        item.EditTranslation("ar", Arabic.Title, Arabic.Summary, "عنوان-تجريبي");
        item.EditTranslation("en", English.Title, English.Summary, "sample-headline");
        item.Publish(Now, Languages);
        return item;
    }

    private static NewsItem Ingest(NewsSource? source = null) =>
        NewsItem.Ingest(source ?? Source(), "https://publisher.example.com/story", "Original title", Now.AddHours(-1), Guid.CreateVersion7(), "original title", null, Now).Value;

    private static NewsSource Source(bool autoPublish = false)
    {
        var source = NewsSourceTests.Create(autoPublish: autoPublish);
        source.Allow(Now);
        return source;
    }
}
