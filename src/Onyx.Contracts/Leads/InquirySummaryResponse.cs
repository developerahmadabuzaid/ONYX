namespace Onyx.Contracts.Leads;

public sealed record InquirySummaryResponse(Guid Id, Guid LeadId, string Type, string LanguageCode, string Name, string? Company, DateTimeOffset SubmittedAtUtc);
