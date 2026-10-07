namespace Onyx.Contracts.News;

public sealed record ChangeNewsCategoryRequest(Guid CategoryId, uint Version);
