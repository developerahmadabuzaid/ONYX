using System.Net;
using System.Net.Sockets;

namespace Onyx.Infrastructure.Outbound;

/// <summary>
/// Which resolved addresses an outbound request may connect to: public unicast only. Blocks loopback, private (RFC 1918, ULA),
/// carrier-grade NAT, link-local (including the 169.254.169.254 cloud metadata endpoint), multicast, documentation, benchmark,
/// reserved and translation ranges, for IPv4 and IPv6. IPv4-mapped/compatible IPv6 addresses are judged by their IPv4 part,
/// and IPv6 transition prefixes that embed arbitrary IPv4 (NAT64, 6to4, Teredo) are refused outright.
/// </summary>
public static class OutboundAddressPolicy
{
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8),          // "this network"
        (IPAddress.Parse("10.0.0.0"), 8),         // private
        (IPAddress.Parse("100.64.0.0"), 10),      // carrier-grade NAT (also some cloud metadata, e.g. 100.100.100.200)
        (IPAddress.Parse("127.0.0.0"), 8),        // loopback
        (IPAddress.Parse("169.254.0.0"), 16),     // link-local, cloud metadata 169.254.169.254
        (IPAddress.Parse("172.16.0.0"), 12),      // private
        (IPAddress.Parse("192.0.0.0"), 24),       // IETF protocol assignments
        (IPAddress.Parse("192.0.2.0"), 24),       // documentation
        (IPAddress.Parse("192.88.99.0"), 24),     // 6to4 relay anycast
        (IPAddress.Parse("192.168.0.0"), 16),     // private
        (IPAddress.Parse("198.18.0.0"), 15),      // benchmarking
        (IPAddress.Parse("198.51.100.0"), 24),    // documentation
        (IPAddress.Parse("203.0.113.0"), 24),     // documentation
        (IPAddress.Parse("224.0.0.0"), 4),        // multicast
        (IPAddress.Parse("240.0.0.0"), 4),        // reserved, broadcast
    ];

    private static readonly (IPAddress Network, int PrefixLength)[] BlockedV6 =
    [
        (IPAddress.Parse("::"), 128),             // unspecified
        (IPAddress.Parse("::1"), 128),            // loopback
        (IPAddress.Parse("64:ff9b::"), 96),       // NAT64 (embeds IPv4)
        (IPAddress.Parse("64:ff9b:1::"), 48),     // local-use NAT64
        (IPAddress.Parse("100::"), 64),           // discard-only
        (IPAddress.Parse("2001::"), 23),          // IETF protocol assignments, incl. Teredo 2001::/32
        (IPAddress.Parse("2001:db8::"), 32),      // documentation
        (IPAddress.Parse("2002::"), 16),          // 6to4 (embeds IPv4)
        (IPAddress.Parse("3fff::"), 20),          // documentation
        (IPAddress.Parse("5f00::"), 16),          // segment routing
        (IPAddress.Parse("fc00::"), 7),           // unique local (incl. fd00:ec2::254 metadata)
        (IPAddress.Parse("fe80::"), 10),          // link-local
        (IPAddress.Parse("fec0::"), 10),          // deprecated site-local
        (IPAddress.Parse("ff00::"), 8),           // multicast
    ];

    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                return IsPublic(address.MapToIPv4());
            }

            var bytes = address.GetAddressBytes();
            if (bytes.Take(12).All(part => part == 0))
            {
                // IPv4-compatible (deprecated) ::a.b.c.d, including :: and ::1.
                return false;
            }

            return address.ScopeId == 0 && !BlockedV6.Any(range => InRange(bytes, range.Network.GetAddressBytes(), range.PrefixLength));
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !BlockedV4.Any(range => InRange(bytes, range.Network.GetAddressBytes(), range.PrefixLength));
        }

        return false;
    }

    private static bool InRange(byte[] address, byte[] network, int prefixLength)
    {
        if (address.Length != network.Length)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (address[index] != network[index])
            {
                return false;
            }
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (address[fullBytes] & mask) == (network[fullBytes] & mask);
    }
}
