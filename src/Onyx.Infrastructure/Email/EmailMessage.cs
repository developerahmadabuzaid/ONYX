using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Email;

/// <summary>
/// A rendered email awaiting delivery by Onyx.Worker. Contains personal data (recipient, body), so it is covered by a
/// retention policy (Stage 9) and never written to logs.
/// </summary>
public sealed class EmailMessage
{
    public const int TemplateKeyMaxLength = 100;
    public const int AddressMaxLength = 254;
    public const int NameMaxLength = 200;
    public const int SubjectMaxLength = 300;
    public const int LockedByMaxLength = 200;
    public const int LastErrorMaxLength = 1000;
    public const int CorrelationIdMaxLength = 64;

    private EmailMessage()
    {
        TemplateKey = string.Empty;
        LanguageCode = string.Empty;
        ToAddress = string.Empty;
        Subject = string.Empty;
        HtmlBody = string.Empty;
        TextBody = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string TemplateKey { get; private set; }

    public string LanguageCode { get; private set; }

    [SensitiveData]
    public string ToAddress { get; private set; }

    [SensitiveData]
    public string? ToName { get; private set; }

    public string Subject { get; private set; }

    [SensitiveData]
    public string HtmlBody { get; private set; }

    [SensitiveData]
    public string TextBody { get; private set; }

    public EmailMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public string? CorrelationId { get; private set; }

    public static EmailMessage Create(
        DateTimeOffset createdAtUtc,
        string templateKey,
        string languageCode,
        string toAddress,
        string? toName,
        string subject,
        string htmlBody,
        string textBody,
        string? correlationId) => new()
    {
        Id = Guid.CreateVersion7(),
        CreatedAtUtc = createdAtUtc,
        TemplateKey = templateKey,
        LanguageCode = languageCode,
        ToAddress = toAddress,
        ToName = toName,
        Subject = subject.Length > SubjectMaxLength ? subject[..SubjectMaxLength] : subject,
        HtmlBody = htmlBody,
        TextBody = textBody,
        Status = EmailMessageStatus.Pending,
        CorrelationId = correlationId is { Length: > CorrelationIdMaxLength } ? correlationId[..CorrelationIdMaxLength] : correlationId,
    };
}
