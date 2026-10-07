namespace Onyx.Application.Search;

/// <summary>What is indexed for one page and language: enough to tell whether it is stale.</summary>
public sealed record IndexedDocumentState(Guid ContentItemId, string LanguageCode, Guid LiveRevisionId, string Path);
