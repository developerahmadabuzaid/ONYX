using System.Collections.Concurrent;
using Onyx.Application.Email;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Test transport. <see cref="Behaviour"/> decides per message whether to succeed or throw.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public const string Key = "email.test";

    public string ProviderKey => Key;

    public bool IsConfigured { get; set; } = true;

    public Func<EmailEnvelope, Task> Behaviour { get; set; } = _ => Task.CompletedTask;

    public ConcurrentQueue<EmailEnvelope> Sent { get; } = new();

    public async Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken)
    {
        await Behaviour(envelope);
        Sent.Enqueue(envelope);
    }
}
