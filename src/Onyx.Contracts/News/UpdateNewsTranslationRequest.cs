namespace Onyx.Contracts.News;

/// <summary>An editor's headline and summary in one language (plain text).</summary>
public sealed record UpdateNewsTranslationRequest(string Title, string Summary, uint Version);
