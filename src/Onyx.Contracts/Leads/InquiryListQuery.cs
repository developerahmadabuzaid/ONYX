namespace Onyx.Contracts.Leads;

public sealed record InquiryListQuery(string? Type = null, int Page = 1, int PageSize = 25);
