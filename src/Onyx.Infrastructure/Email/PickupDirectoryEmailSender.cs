using Microsoft.Extensions.Options;
using Onyx.Application.Email;
using Onyx.Application.Providers;

namespace Onyx.Infrastructure.Email;

/// <summary>Development-only transport: writes each message as an .eml file so emails can be inspected without sending anything.</summary>
public sealed class PickupDirectoryEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public string ProviderKey => ProviderKeys.EmailPickupDirectory;

    public bool IsConfigured => options.Value is { Transport: EmailTransport.PickupDirectory, PickupDirectory: { Length: > 0 }, FromAddress: { Length: > 0 } };

    public async Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var settings = options.Value;
        using var message = MimeMessageFactory.Create(envelope, settings);

        try
        {
            Directory.CreateDirectory(settings.PickupDirectory!);
            var path = Path.Combine(settings.PickupDirectory!, $"{envelope.MessageId:N}.eml");
            await message.WriteToAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new EmailTransportException(EmailFailureKind.ProviderUnavailable, "Pickup directory is not writable.", exception);
        }
    }
}
