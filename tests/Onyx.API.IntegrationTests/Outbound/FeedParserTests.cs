using System.Text;
using Onyx.Application.News;
using Onyx.Domain.News;
using Onyx.Infrastructure.News;

namespace Onyx.API.IntegrationTests.Outbound;

/// <summary>Parsing untrusted feeds: well-formed RSS/Atom are read; XXE, entity bombs, wrong formats and junk are rejected safely.</summary>
public sealed class FeedParserTests
{
    private const string Rss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0">
          <channel>
            <title>Sample feed</title>
            <link>https://publisher.example.com/</link>
            <description>Fictional sample feed for tests.</description>
            <item>
              <title>Sample &amp; story &lt;b&gt;one&lt;/b&gt;</title>
              <link>https://publisher.example.com/story-1?utm_source=rss</link>
              <pubDate>Tue, 06 Oct 2026 08:30:00 GMT</pubDate>
              <category>Cybersecurity</category>
              <description><![CDATA[<p>First <script>alert(1)</script>paragraph &amp; more.</p>]]></description>
            </item>
            <item>
              <title>Sample story two</title>
              <guid isPermaLink="true">https://publisher.example.com/story-2</guid>
              <pubDate>not a date</pubDate>
            </item>
            <item>
              <title>Sample story three</title>
              <link>javascript:alert(1)</link>
              <pubDate>Tue, 06 Oct 2026 10:00:00 +0300</pubDate>
            </item>
          </channel>
        </rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <title>Sample Atom feed</title>
          <id>urn:uuid:00000000-0000-0000-0000-000000000001</id>
          <updated>2026-10-06T08:00:00Z</updated>
          <entry>
            <title>Atom sample story</title>
            <link rel="alternate" href="https://publisher.example.com/atom-1"/>
            <id>urn:uuid:00000000-0000-0000-0000-000000000002</id>
            <updated>2026-10-06T09:15:00+02:00</updated>
            <summary type="html">&lt;p&gt;Atom summary&lt;/p&gt;</summary>
          </entry>
        </feed>
        """;

    [Fact]
    public void Rss_entries_are_read_as_plain_text_with_truthful_dates_and_links()
    {
        var entries = Parse(Rss, NewsFeedFormat.Rss);

        Assert.Equal(3, entries.Count);
        Assert.Equal("Sample & story one", entries[0].Title);
        Assert.Equal(new Uri("https://publisher.example.com/story-1?utm_source=rss"), entries[0].Link);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 8, 30, 0, TimeSpan.Zero), entries[0].PublishedAtUtc);
        Assert.Equal(["Cybersecurity"], entries[0].Categories);
        Assert.Equal("First alert(1) paragraph & more.", entries[0].Excerpt);      // text only: tags gone, never executed or stored
        Assert.DoesNotContain("<", entries[0].Excerpt!, StringComparison.Ordinal);

        Assert.Equal(new Uri("https://publisher.example.com/story-2"), entries[1].Link);   // permalink guid
        Assert.Null(entries[1].PublishedAtUtc);                                             // unusable date is not invented
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 7, 0, 0, TimeSpan.Zero), entries[2].PublishedAtUtc);
        Assert.False(entries[2].Link is { Scheme: "http" or "https" });                     // rejected later by NewsUrl
    }

    [Fact]
    public void Atom_entries_are_read()
    {
        var entry = Assert.Single(Parse(Atom, NewsFeedFormat.Atom));

        Assert.Equal("Atom sample story", entry.Title);
        Assert.Equal(new Uri("https://publisher.example.com/atom-1"), entry.Link);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 7, 15, 0, TimeSpan.Zero), entry.PublishedAtUtc);
        Assert.Equal("Atom summary", entry.Excerpt);
    }

    [Fact]
    public void Entries_are_capped()
    {
        var result = SyndicationNewsFeedReader.Parse(Encoding.UTF8.GetBytes(Rss), NewsFeedFormat.Rss, maxEntries: 1);

        Assert.Single(result.Value);
    }

    [Theory]
    [InlineData("""<?xml version="1.0"?><!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><rss version="2.0"><channel><title>&xxe;</title></channel></rss>""")]
    [InlineData("""<?xml version="1.0"?><!DOCTYPE rss [<!ENTITY xxe SYSTEM "http://169.254.169.254/latest/meta-data/">]><rss version="2.0"><channel><title>&xxe;</title></channel></rss>""")]
    [InlineData("""<?xml version="1.0"?><!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;"><!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">]><rss version="2.0"><channel><title>&lol3;</title></channel></rss>""")]
    [InlineData("<html><body>Not a feed</body></html>")]
    [InlineData("""{"title": "json is not a feed"}""")]
    [InlineData("<rss version=\"2.0\"><channel><title>Unclosed")]
    [InlineData("")]
    public void Dtds_external_entities_entity_bombs_and_non_feeds_are_rejected(string document)
    {
        var result = SyndicationNewsFeedReader.Parse(Encoding.UTF8.GetBytes(document), NewsFeedFormat.Rss, 50);

        Assert.True(result.IsFailure);
        Assert.Equal(NewsFeedErrors.Malformed, result.Error!.Code);
    }

    [Fact]
    public void A_feed_in_the_other_format_is_rejected()
    {
        Assert.Equal(NewsFeedErrors.Malformed, SyndicationNewsFeedReader.Parse(Encoding.UTF8.GetBytes(Atom), NewsFeedFormat.Rss, 50).Error!.Code);
        Assert.Equal(NewsFeedErrors.Malformed, SyndicationNewsFeedReader.Parse(Encoding.UTF8.GetBytes(Rss), NewsFeedFormat.Atom, 50).Error!.Code);
    }

    [Theory]
    [InlineData("Tue, 06 Oct 2026 08:30:00 GMT", "2026-10-06T08:30:00Z")]
    [InlineData("Tue, 06 Oct 2026 08:30:00 +0300", "2026-10-06T05:30:00Z")]
    [InlineData("Tue, 06 Oct 2026 08:30:00 EDT", "2026-10-06T12:30:00Z")]
    [InlineData("2026-10-06T08:30:00+02:00", "2026-10-06T06:30:00Z")]
    [InlineData("2026-10-06T08:30:00Z", "2026-10-06T08:30:00Z")]
    [InlineData("yesterday", null)]
    [InlineData("", null)]
    public void Dates_are_parsed_or_left_unknown(string value, string? expected) =>
        Assert.Equal(expected is null ? null : DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), FeedText.ParseDate(value));

    private static List<FeedEntry> Parse(string document, NewsFeedFormat format)
    {
        var result = SyndicationNewsFeedReader.Parse(Encoding.UTF8.GetBytes(document.Trim()), format, 50);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value.ToList();
    }
}
