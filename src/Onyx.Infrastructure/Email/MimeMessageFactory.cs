using System.Text;
using MimeKit;
using Onyx.Application.Email;

namespace Onyx.Infrastructure.Email;

internal static class MimeMessageFactory
{
    public static MimeMessage Create(EmailEnvelope envelope, EmailOptions options)
    {
        if (!MailboxAddress.TryParse(envelope.ToAddress, out var recipient))
        {
            throw new EmailTransportException(EmailFailureKind.MessageRejected, "Recipient address is not valid.");
        }

        var sender = MailboxAddress.Parse(options.FromAddress!);
        sender.Name = options.FromName;
        if (!string.IsNullOrWhiteSpace(envelope.ToName))
        {
            recipient.Name = envelope.ToName;
        }

        var message = new MimeMessage
        {
            Subject = envelope.Subject,
            MessageId = $"{envelope.MessageId:N}@{sender.Domain}",
        };
        message.From.Add(sender);
        message.To.Add(recipient);
        var body = new BodyBuilder { HtmlBody = envelope.HtmlBody, TextBody = envelope.TextBody };
        foreach (var attachment in envelope.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, Encoding.UTF8.GetBytes(attachment.Content), ContentType.Parse(attachment.ContentType + "; charset=utf-8"));
        }

        message.Body = body.ToMessageBody();

        // Content-* headers belong to the MIME entity, not the message envelope.
        message.Body.Headers.Replace(HeaderId.ContentLanguage, envelope.LanguageCode);
        return message;
    }
}
