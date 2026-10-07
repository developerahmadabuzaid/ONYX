using Onyx.Domain.News;

namespace Onyx.Domain.Tests.News;

public sealed class NewsSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_source_is_blocked_and_cannot_be_allowed_without_a_reviewed_licensing_note()
    {
        var source = Create(licensingNote: null);

        Assert.Equal(NewsSourceStatus.Blocked, source.Status);
        Assert.False(source.IsDue(Now));
        Assert.Equal("News.LicensingNoteRequired", source.Allow(Now).Error!.Code);

        Assert.True(source.Update("Sample Publisher", "https://publisher.example.com", "https://publisher.example.com/feed.xml", NewsFeedFormat.Rss, "en",
            Guid.CreateVersion7(), 60, "Terms reviewed (sample).", autoPublish: false).IsSuccess);
        Assert.True(source.Allow(Now).IsSuccess);
        Assert.True(source.IsDue(Now));
    }

    [Fact]
    public void Removing_the_licensing_note_blocks_the_source_again()
    {
        var source = Create();
        source.Allow(Now);

        source.Update(source.Name, source.HomepageUrl, source.FeedUrl, source.FeedFormat, source.LanguageCode, source.DefaultCategoryId, 60, null, false);

        Assert.Equal(NewsSourceStatus.Blocked, source.Status);
    }

    [Theory]
    [InlineData("http://publisher.example.com/feed.xml")]
    [InlineData("https://10.0.0.8/feed.xml")]
    [InlineData("https://localhost/feed.xml")]
    public void Feed_urls_must_be_public_https(string feedUrl) =>
        Assert.Equal("News.InvalidFeedUrl", NewsSource.Create("Sample", "https://publisher.example.com", feedUrl, NewsFeedFormat.Rss, "en", Guid.CreateVersion7(), 60, null, false).Error!.Code);

    [Fact]
    public void Failures_back_off_exponentially_and_only_the_first_failure_notifies()
    {
        var source = Create();
        source.Allow(Now);

        source.RecordFailure(Now, "Feed.Timeout: The request timed out.");
        Assert.Equal(Now.AddMinutes(60), source.NextFetchAtUtc);
        source.RecordFailure(Now, "Feed.Timeout: The request timed out.");
        Assert.Equal(Now.AddMinutes(120), source.NextFetchAtUtc);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            source.RecordFailure(Now, "Feed.Timeout: The request timed out.");
        }

        Assert.Equal(Now.Add(NewsSource.MaxBackoff), source.NextFetchAtUtc);
        Assert.Single(source.DomainEvents.OfType<NewsSourceFailing>());

        source.RecordSuccess(Now);
        Assert.Equal(0, source.ConsecutiveFailures);
        Assert.Null(source.LastError);
        Assert.Equal(Now.AddMinutes(60), source.NextFetchAtUtc);
    }

    internal static NewsSource Create(string? licensingNote = "Terms reviewed (sample).", bool autoPublish = false) =>
        NewsSource.Create(
            "Sample Publisher", "https://publisher.example.com", "https://publisher.example.com/feed.xml", NewsFeedFormat.Rss, "en", Guid.CreateVersion7(), 60,
            licensingNote, autoPublish).Value;
}
