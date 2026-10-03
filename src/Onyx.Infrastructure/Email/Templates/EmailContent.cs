namespace Onyx.Infrastructure.Email.Templates;

/// <summary>Plain-text building blocks of an email. Every string is HTML-encoded by the renderer.</summary>
public sealed record EmailContent(string Subject, string Heading, IReadOnlyList<string> Paragraphs, EmailAction? Action, string? Footer);
