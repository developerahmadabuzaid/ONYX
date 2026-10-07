namespace Onyx.Contracts.News;

public sealed record NewsItemListQuery(string? Status = null, Guid? SourceId = null, Guid? CategoryId = null, int Page = 1, int PageSize = 25);
