using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Onyx.Application.Email;
using Onyx.Application.Providers;

namespace Onyx.Infrastructure.Email;

/// <summary>
/// SMTP transport via MailKit (also used for Microsoft 365 SMTP AUTH). Certificate validation is never disabled.
/// Failures are reported as <see cref="EmailTransportException"/> with credential-free messages.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public string ProviderKey => ProviderKeys.EmailSmtp;

    public bool IsConfigured => options.Value is { Transport: EmailTransport.Smtp, Smtp.Host: { Length: > 0 }, FromAddress: { Length: > 0 } };

    public async Task SendAsync(EmailEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var settings = options.Value;
        if (!IsConfigured)
        {
            throw new EmailTransportException(EmailFailureKind.ProviderUnavailable, "SMTP transport is not configured.");
        }

        var host = settings.Smtp.Host!;
        using var message = MimeMessageFactory.Create(envelope, settings);
        using var client = new SmtpClient { Timeout = settings.Smtp.TimeoutSeconds * 1000 };

        try
        {
            await client.ConnectAsync(host, settings.Smtp.Port, ToSocketOptions(settings.Smtp.Security), cancellationToken).ConfigureAwait(false);
            if (settings.Smtp is { Username: { Length: > 0 } username, Password: { Length: > 0 } password })
            {
                await client.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
        }
        catch (SmtpCommandException exception) when (exception.ErrorCode is SmtpErrorCode.RecipientNotAccepted)
        {
            throw new EmailTransportException(EmailFailureKind.MessageRejected, $"Recipient rejected by the SMTP server ({(int)exception.StatusCode}).", exception);
        }
        catch (SmtpCommandException exception) when (exception.ErrorCode is SmtpErrorCode.MessageNotAccepted)
        {
            throw new EmailTransportException(EmailFailureKind.MessageRejected, $"Message rejected by the SMTP server ({(int)exception.StatusCode}).", exception);
        }
        catch (AuthenticationException exception)
        {
            throw new EmailTransportException(EmailFailureKind.ProviderUnavailable, "SMTP authentication failed.", exception);
        }
        catch (Exception exception) when (exception is SmtpCommandException or SmtpProtocolException or ServiceNotConnectedException
            or SocketException or IOException or SslHandshakeException or TimeoutException)
        {
            throw new EmailTransportException(EmailFailureKind.ProviderUnavailable, "SMTP server unreachable or refused the connection.", exception);
        }
    }

    private static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls,
    };
}
