namespace Onyx.Domain.Content;

/// <summary>A run of plain text, optionally emphasised or linked.</summary>
public sealed record TextSpan(string Text, TextEmphasis Emphasis = TextEmphasis.None, LinkTarget? Link = null);
