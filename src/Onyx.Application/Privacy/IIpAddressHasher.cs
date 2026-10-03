namespace Onyx.Application.Privacy;

/// <summary>Keyed, non-reversible IP hashing for privacy records. Returns null when no key is configured (the IP is then not stored at all).</summary>
public interface IIpAddressHasher
{
    string? Hash(string? ipAddress);
}
