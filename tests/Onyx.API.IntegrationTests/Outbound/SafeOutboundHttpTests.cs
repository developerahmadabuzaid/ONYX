using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Onyx.Application.News;
using Onyx.Infrastructure.Outbound;

namespace Onyx.API.IntegrationTests.Outbound;

/// <summary>
/// SSRF suite for the outbound gateway, against a real local HTTP server. The fake DNS decides what each host "resolves" to;
/// the connector proves which addresses the client actually tried to reach (never a non-public one).
/// </summary>
public sealed class SafeOutboundHttpTests : IAsyncLifetime
{
    private const string Feed = """<?xml version="1.0"?><rss version="2.0"><channel><title>Sample</title></channel></rss>""";

    private static readonly SafeOutboundRequest Http = new(RequireHttps: false, "application/rss+xml");

    private OutboundTestServer _server = null!;
    private FakeDnsResolver _dns = null!;
    private LoopbackConnector _connector = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _server = await OutboundTestServer.StartAsync();
        _server.Bodies["/feed.xml"] = Feed;
        _dns = new FakeDnsResolver()
            .Map("feeds.publisher-sample.com", OutboundTestServer.PublicAddress)
            .Map("internal.publisher-sample.com", "10.0.0.5");
        _connector = new LoopbackConnector(_server.Port);
    }

    public async ValueTask DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task A_public_destination_is_fetched_with_an_identifiable_agent_and_no_cookies()
    {
        using var client = Client();
        var response = await client.GetAsync(new Uri("http://feeds.publisher-sample.com/feed.xml"), Http, Token);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(Feed, System.Text.Encoding.UTF8.GetString(response.Body));
        Assert.Equal([IPAddress.Parse(OutboundTestServer.PublicAddress)], _connector.Connected);
        Assert.Equal("OnyxVanguard-NewsReader", client.UserAgentProduct);
    }

    [Theory]
    [InlineData("http://localhost/feed.xml")]
    [InlineData("http://127.0.0.1/feed.xml")]
    [InlineData("http://[::1]/feed.xml")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://2130706433/feed.xml")]
    [InlineData("http://0x7f.0x0.0x0.0x1/feed.xml")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("http://user:pass@feeds.publisher-sample.com/feed.xml")]
    [InlineData("http://feeds.publisher-sample.com:8080/feed.xml")]
    [InlineData("ftp://feeds.publisher-sample.com/feed.xml")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://feeds.publisher-sample.com/")]
    public async Task Forbidden_urls_are_refused_before_any_connection(string url)
    {
        using var client = Client();

        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri(url), Http, Token));

        Assert.Equal(NewsFeedErrors.Blocked, refused.Code);
        Assert.Empty(_connector.Connected);
        Assert.Empty(_server.RequestedPaths);
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.4.2")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("100.100.100.200")]    // Alibaba Cloud metadata
    [InlineData("169.254.169.254")]    // AWS/Azure/GCP metadata
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fd00:ec2::254")]      // AWS IPv6 metadata (unique local)
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]   // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::127.0.0.1")]        // IPv4-compatible
    [InlineData("64:ff9b::a00:1")]     // NAT64 of 10.0.0.1
    [InlineData("2002:a00:1::1")]      // 6to4 of 10.0.0.1
    [InlineData("2001::1")]            // Teredo
    public async Task Hosts_resolving_to_non_public_addresses_are_never_connected(string address)
    {
        _dns.Map("rebound.publisher-sample.com", address);
        using var client = Client();

        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://rebound.publisher-sample.com/feed.xml"), Http, Token));

        Assert.Equal(NewsFeedErrors.Blocked, refused.Code);
        Assert.Empty(_connector.Connected);
    }

    [Fact]
    public async Task A_mixed_answer_with_any_private_address_is_refused_entirely()
    {
        _dns.Map("mixed.publisher-sample.com", OutboundTestServer.PublicAddress, "10.0.0.7");
        using var client = Client();

        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://mixed.publisher-sample.com/feed.xml"), Http, Token));

        Assert.Equal(NewsFeedErrors.Blocked, refused.Code);
        Assert.Empty(_connector.Connected);
    }

    [Fact]
    public async Task Dns_rebinding_cannot_reach_an_internal_address_on_a_later_connection()
    {
        // First lookup answers public, the next one private (a rebinding attacker's DNS).
        _dns.Map("rebind.publisher-sample.com", OutboundTestServer.PublicAddress).Map("rebind.publisher-sample.com", "127.0.0.1");
        using var first = Client();
        using var second = Client();

        var allowed = await first.GetAsync(new Uri("http://rebind.publisher-sample.com/feed.xml"), Http, Token);
        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => second.GetAsync(new Uri("http://rebind.publisher-sample.com/feed.xml"), Http, Token));

        Assert.Equal(200, allowed.StatusCode);
        Assert.Equal(NewsFeedErrors.Blocked, refused.Code);
        Assert.All(_connector.Connected, address => Assert.True(OutboundAddressPolicy.IsPublic(address)));
        Assert.Single(_connector.Connected);
    }

    [Theory]
    [InlineData("/redirect-to-private")]
    [InlineData("/redirect-to-metadata")]
    [InlineData("/redirect-to-file")]
    public async Task Redirects_to_forbidden_destinations_are_refused(string path)
    {
        using var client = Client();

        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri($"http://feeds.publisher-sample.com{path}"), Http, Token));

        Assert.Equal(NewsFeedErrors.Blocked, refused.Code);
        Assert.All(_connector.Connected, address => Assert.Equal(IPAddress.Parse(OutboundTestServer.PublicAddress), address));
    }

    [Fact]
    public async Task Redirect_chains_are_followed_within_the_limit_and_refused_beyond_it()
    {
        using var client = Client();

        var followed = await client.GetAsync(new Uri("http://feeds.publisher-sample.com/redirect-ok"), Http, Token);
        var looped = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://feeds.publisher-sample.com/redirect-loop"), Http, Token));

        Assert.Equal(new Uri("http://feeds.publisher-sample.com/feed.xml"), followed.FinalUrl);
        Assert.Equal(NewsFeedErrors.TooManyRedirects, looped.Code);
        Assert.Equal(1 + 4, _server.RequestedPaths.Count(path => path is "/redirect-ok" or "/redirect-loop"));
    }

    [Fact]
    public void Https_is_enforced_when_required_including_on_redirect_targets()
    {
        Assert.Equal(NewsFeedErrors.Blocked, Assert.Throws<SafeOutboundException>(() => SafeOutboundHttpClient.Validate(new Uri("http://feeds.publisher-sample.com/feed.xml"), requireHttps: true)).Code);
        Assert.Equal(new Uri("https://feeds.publisher-sample.com/feed.xml"), SafeOutboundHttpClient.Validate(new Uri("https://feeds.publisher-sample.com/feed.xml#top"), requireHttps: true));
    }

    [Theory]
    [InlineData("/large")]
    [InlineData("/large-chunked")]
    [InlineData("/gzip-bomb")]
    public async Task Oversized_and_decompression_bomb_responses_are_cut_off(string path)
    {
        using var client = Client();

        var refused = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri($"http://feeds.publisher-sample.com{path}"), Http, Token));

        Assert.Equal(NewsFeedErrors.TooLarge, refused.Code);
    }

    [Fact]
    public async Task Slow_responses_time_out_and_error_statuses_are_reported_without_content()
    {
        using var client = Client(timeoutSeconds: 1);

        var slow = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://feeds.publisher-sample.com/slow"), Http, Token));
        var failed = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://feeds.publisher-sample.com/status-500"), Http, Token));
        var unknown = await Assert.ThrowsAsync<SafeOutboundException>(() => client.GetAsync(new Uri("http://unknown.publisher-sample.com/feed.xml"), Http, Token));

        Assert.Equal(NewsFeedErrors.Timeout, slow.Code);
        Assert.Equal(NewsFeedErrors.HttpStatus, failed.Code);
        Assert.Equal(500, failed.StatusCode);
        Assert.Equal(NewsFeedErrors.Unreachable, unknown.Code);
        Assert.DoesNotContain("publisher-sample", slow.Message + failed.Message + unknown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Robots_txt_is_honoured_and_a_missing_file_allows_fetching()
    {
        using var client = Client();
        var robots = new RobotsTxtPolicy(client, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        _server.Bodies["/robots.txt"] = "User-agent: *\nAllow: /\n\nUser-agent: OnyxVanguard-NewsReader\nDisallow: /private/\nAllow: /private/feed.xml$\n";

        Assert.True(await robots.IsAllowedAsync(new Uri("http://feeds.publisher-sample.com/feed.xml"), requireHttps: false, Token));
        Assert.False(await robots.IsAllowedAsync(new Uri("http://feeds.publisher-sample.com/private/other.xml"), requireHttps: false, Token));
        Assert.True(await robots.IsAllowedAsync(new Uri("http://feeds.publisher-sample.com/private/feed.xml"), requireHttps: false, Token));

        _dns.Map("norobots.publisher-sample.com", OutboundTestServer.PublicAddress);
        _server.Bodies.TryRemove("/robots.txt", out _);
        Assert.True(await robots.IsAllowedAsync(new Uri("http://norobots.publisher-sample.com/feed.xml"), requireHttps: false, Token));
    }

    [Theory]
    [InlineData("User-agent: *\nDisallow: /", "/feed.xml", false)]
    [InlineData("User-agent: *\nDisallow: /", "/", false)]
    [InlineData("User-agent: *\nDisallow:", "/feed.xml", true)]
    [InlineData("User-agent: OtherBot\nDisallow: /", "/feed.xml", true)]
    [InlineData("User-agent: onyxvanguard-newsreader\nDisallow: /*.xml$", "/feed.xml", false)]
    [InlineData("User-agent: onyxvanguard-newsreader\nDisallow: /*.xml$", "/feed.xml?x=1", true)]
    [InlineData("User-agent: *\nDisallow: /feeds\nAllow: /feeds/public", "/feeds/public/rss", true)]
    [InlineData("# comment only", "/feed.xml", true)]
    public void Robots_rules_follow_rfc_9309(string robots, string path, bool allowed) =>
        Assert.Equal(allowed, RobotsTxtRules.Parse(robots, "OnyxVanguard-NewsReader").IsAllowed(path));

    private SafeOutboundHttpClient Client(int timeoutSeconds = 10) =>
        new(
            Options.Create(new SafeOutboundHttpOptions { TimeoutSeconds = timeoutSeconds, MaxResponseBytes = 2 * 1_024 * 1_024, MaxRedirects = 3 }),
            _dns,
            _connector,
            NullLogger<SafeOutboundHttpClient>.Instance);
}
