namespace Onyx.Infrastructure.Outbound;

/// <summary>A completed, size-capped response. <see cref="Body"/> is held in memory only for the caller's processing.</summary>
public sealed record SafeOutboundResponse(int StatusCode, string? MediaType, Uri FinalUrl, byte[] Body);
