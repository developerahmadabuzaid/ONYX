namespace Onyx.Contracts.Leads;

public sealed record LeadNoteResponse(Guid Id, string Body, Guid AuthorId, string? AuthorName, DateTimeOffset CreatedAtUtc);
