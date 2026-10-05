namespace Onyx.Application.Privacy;

/// <summary>An email sent (or queued) to the subject, as included in an export.</summary>
public sealed record SubjectEmailRecord(
    string TemplateKey,
    string LanguageCode,
    string Subject,
    string TextBody,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc);
