namespace Onyx.Contracts.News;

public sealed record SetNewsIndexingRequest(bool Indexable, uint Version);
