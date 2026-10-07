namespace Onyx.Domain.News;

/// <summary>A headline and summary in one language.</summary>
public sealed record NewsText(string LanguageCode, string Title, string Summary);
