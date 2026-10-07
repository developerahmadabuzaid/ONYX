using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Onyx.Application.News;

namespace Onyx.Infrastructure.Outbound;

/// <summary>
/// Honours a site's robots.txt before Onyx fetches from it (CLAUDE.md §8). The file is fetched through the same safe client,
/// cached per origin for an hour, and read with the agent's product token. A missing file (4xx) allows everything; a server
/// error or an unreachable file means "try later" (the fetch is not attempted), never "allowed".
/// </summary>
public sealed class RobotsTxtPolicy(SafeOutboundHttpClient http, IMemoryCache cache)
{
    public const int MaxRobotsBytes = 512 * 1_024;

    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    public async Task<bool> IsAllowedAsync(Uri url, bool requireHttps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var origin = $"{url.Scheme}://{url.IdnHost}";
        var key = $"onyx.robots:{origin}";
        if (!cache.TryGetValue(key, out RobotsTxtRules? rules) || rules is null)
        {
            rules = await LoadAsync(new Uri($"{origin}/robots.txt"), requireHttps, cancellationToken).ConfigureAwait(false);
            cache.Set(key, rules, CacheFor);
        }

        return rules.IsAllowed(url.PathAndQuery);
    }

    private async Task<RobotsTxtRules> LoadAsync(Uri robotsUrl, bool requireHttps, CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.GetAsync(robotsUrl, new SafeOutboundRequest(requireHttps, "text/plain", MaxRobotsBytes), cancellationToken).ConfigureAwait(false);
            return RobotsTxtRules.Parse(Encoding.UTF8.GetString(response.Body), http.UserAgentProduct);
        }
        catch (SafeOutboundException exception) when (exception.Code == NewsFeedErrors.HttpStatus && exception.StatusCode is >= 400 and < 500)
        {
            return RobotsTxtRules.AllowAll;
        }
    }
}
