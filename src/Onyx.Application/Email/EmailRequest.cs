namespace Onyx.Application.Email;

/// <summary>
/// An email to send through a code-defined template. <see cref="LanguageCode"/> must be the language of the submission
/// that triggered it. Model values are plain text and are always HTML-encoded by the renderer.
/// </summary>
public sealed record EmailRequest(
    string TemplateKey,
    string LanguageCode,
    string ToAddress,
    string? ToName,
    IReadOnlyDictionary<string, string> Model,
    IReadOnlyList<EmailAttachment>? Attachments = null);
