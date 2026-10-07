using System.Net;

namespace Onyx.Infrastructure.Outbound;

/// <summary>Resolves a host name once per connection attempt (replaceable in tests to simulate private answers and rebinding).</summary>
public interface IOutboundDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
