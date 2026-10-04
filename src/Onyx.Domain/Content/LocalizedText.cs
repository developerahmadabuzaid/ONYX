namespace Onyx.Domain.Content;

/// <summary>A short text authored per language (labels, alt text, statements). Stored inside a JSON column of its owner.</summary>
public sealed record LocalizedText(string LanguageCode, string Text, string? Secondary = null);
