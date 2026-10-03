namespace Onyx.Application.Email;

/// <summary>A fully rendered message handed to the transport.</summary>
public sealed record EmailEnvelope(
    Guid MessageId,
    string ToAddress,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    string LanguageCode);
