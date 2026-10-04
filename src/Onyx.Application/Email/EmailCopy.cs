namespace Onyx.Application.Email;

/// <summary>The editable wording of an email in one language: plain text with <c>{placeholder}</c> tokens.</summary>
public sealed record EmailCopy(string Subject, string Heading, IReadOnlyList<string> Paragraphs, string? ActionLabel, string? Footer);
