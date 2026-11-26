namespace Onyx.Contracts.SiteContent;

/// <summary>Optimistic-concurrency token for publish, discard and restore.</summary>
public sealed record SiteContentVersionRequest(uint Version);
