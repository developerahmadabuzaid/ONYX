namespace Onyx.Application.Knowledge;

/// <summary>One approved, sourced statement made available to AI writing, with its id for traceability.</summary>
public sealed record GroundingFact(Guid KnowledgeEntryId, string Category, string Title, string Statement);
