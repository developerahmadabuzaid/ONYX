using Onyx.Application.Email;
using Onyx.Application.Providers;

namespace Onyx.Infrastructure.Email;

/// <summary>Used while no transport is configured. Never sends; the dispatcher leaves messages queued.</summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    public string ProviderKey => ProviderKeys.EmailSmtp;

    public bool IsConfigured => false;

    public Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No email transport is configured.");
}
