namespace Onyx.Contracts.News;

/// <summary>A state change on a news source or item, guarded by its concurrency version.</summary>
public sealed record NewsVersionRequest(uint Version);
