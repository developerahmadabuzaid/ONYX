namespace Onyx.Application.Search;

/// <summary>What is indexed for one page (or news item) and language: enough to tell whether it is stale, and which indexer owns it.</summary>
public sealed record IndexedDocumentState(Guid ContentItemId, string LanguageCode, Guid LiveRevisionId, string Path, string Group);
