using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Revision metadata without its snapshot, so long histories list cheaply.</summary>
public sealed record RevisionSummary(Guid Id, int Number, RevisionKind Kind, string? Note, DateTimeOffset CreatedAtUtc, Guid? CreatedById, Guid? RestoredFromRevisionId);
