namespace Onyx.Contracts.Privacy;

public sealed record DataSubjectRequestListQuery(string? Status = null, int Page = 1, int PageSize = 25);
