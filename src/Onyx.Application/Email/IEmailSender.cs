namespace Onyx.Application.Email;

/// <summary>Email transport (SMTP, Microsoft 365 SMTP, a transactional provider later). Used only by Onyx.Worker.</summary>
public interface IEmailSender
{
    /// <summary>Key under which transport health is tracked in provider connections.</summary>
    string ProviderKey { get; }

    /// <summary>False when no transport credentials are configured: messages stay queued instead of failing.</summary>
    bool IsConfigured { get; }

    Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken);
}
