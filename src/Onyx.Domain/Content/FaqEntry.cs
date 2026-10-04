namespace Onyx.Domain.Content;

/// <summary>A visible question and answer, authored per language. Plain text; the answer may contain line breaks.</summary>
public sealed record FaqEntry(string Question, string Answer);
