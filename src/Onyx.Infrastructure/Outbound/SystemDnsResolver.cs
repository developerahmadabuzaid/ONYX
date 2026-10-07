using System.Net;

namespace Onyx.Infrastructure.Outbound;

internal sealed class SystemDnsResolver : IOutboundDnsResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken);
}
