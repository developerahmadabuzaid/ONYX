using Onyx.Domain.News;

namespace Onyx.Domain.Tests.News;

public sealed class NewsUrlTests
{
    [Theory]
    [InlineData("https://feeds.publisher.com/tech.xml")]
    [InlineData("https://news.publisher.co.uk/rss?section=ai")]
    [InlineData("https://cafe.be/feed")]
    [InlineData("https://مثال.السعودية/rss")]
    public void Public_https_urls_on_domain_names_are_accepted(string url) =>
        Assert.True(NewsUrl.Parse(url, requireHttps: true).IsSuccess);

    [Theory]
    [InlineData("http://feeds.publisher.com/tech.xml")]                 // plain http where https is required
    [InlineData("https://user:secret@feeds.publisher.com/feed")]        // embedded credentials
    [InlineData("https://feeds.publisher.com:8443/feed")]               // non-default port
    [InlineData("https://localhost/feed")]
    [InlineData("https://intranet.localhost/feed")]
    [InlineData("https://printer.local/feed")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("https://router.home.arpa/feed")]
    [InlineData("https://127.0.0.1/feed")]                              // IP literals, any form
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://[::1]/feed")]
    [InlineData("https://[fd00:ec2::254]/feed")]
    [InlineData("https://2130706433/feed")]                             // decimal 127.0.0.1
    [InlineData("https://0x7f.0x0.0x0.0x1/feed")]                       // hex
    [InlineData("https://127.1/feed")]                                  // short form
    [InlineData("https://intranet/feed")]                               // single label
    [InlineData("ftp://feeds.publisher.com/feed")]                      // other schemes
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("gopher://feeds.publisher.com/")]
    [InlineData("/relative/feed")]
    [InlineData("")]
    public void Internal_unusual_and_non_http_destinations_are_refused(string url) =>
        Assert.True(NewsUrl.Parse(url, requireHttps: true).IsFailure, url);

    [Fact]
    public void Article_links_may_be_http_but_never_internal()
    {
        Assert.True(NewsUrl.Check(new Uri("http://publisher.com/story"), requireHttps: false).IsSuccess);
        Assert.True(NewsUrl.Check(new Uri("http://10.0.0.5/story"), requireHttps: false).IsFailure);
    }

    [Fact]
    public void Canonical_form_ignores_case_fragments_default_ports_and_tracking_parameters()
    {
        var first = NewsUrl.Canonicalize(new Uri("https://Publisher.COM:443/Story/1?utm_source=x&id=7&fbclid=abc#comments"));
        var second = NewsUrl.Canonicalize(new Uri("https://publisher.com/Story/1?id=7"));

        Assert.Equal("https://publisher.com/Story/1?id=7", first);
        Assert.Equal(first, second);
        Assert.Equal(NewsUrl.Hash(first), NewsUrl.Hash(second));
        Assert.Equal(NewsUrl.HashLength, NewsUrl.Hash(first).Length);
        Assert.NotEqual(NewsUrl.Hash(first), NewsUrl.Hash("https://publisher.com/Story/2?id=7"));
    }
}
