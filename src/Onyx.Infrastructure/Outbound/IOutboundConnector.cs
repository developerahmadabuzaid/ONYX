using System.Net;

namespace Onyx.Infrastructure.Outbound;

/// <summary>
/// Opens the TCP connection to an address that has already passed <see cref="OutboundAddressPolicy"/> (replaceable in tests,
/// which route vetted public addresses to a local test server).
/// </summary>
public interface IOutboundConnector
{
    ValueTask<Stream> ConnectAsync(IPAddress address, int port, CancellationToken cancellationToken);
}
