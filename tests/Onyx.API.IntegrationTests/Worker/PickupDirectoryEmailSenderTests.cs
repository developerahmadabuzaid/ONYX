using Microsoft.Extensions.Options;
using MimeKit;
using Onyx.Application.Email;
using Onyx.Infrastructure.Email;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class PickupDirectoryEmailSenderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"onyx-pickup-{Guid.NewGuid():N}");

    [Fact]
    public async Task Writes_a_complete_mime_message_with_both_bodies_and_language()
    {
        var sender = CreateSender();
        var envelope = new EmailEnvelope(Guid.NewGuid(), "client@example.com", "عميل", "رسالة اختبار", "<p>مرحباً</p>", "مرحباً", "ar");

        await sender.SendAsync(envelope, TestContext.Current.CancellationToken);

        var path = Assert.Single(Directory.GetFiles(_directory, "*.eml"));
        using var message = await MimeMessage.LoadAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("رسالة اختبار", message.Subject);
        Assert.Equal("client@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("عميل", message.To.Mailboxes.Single().Name);
        Assert.Equal("no-reply@example.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("ar", message.Body?.Headers[HeaderId.ContentLanguage]);
        Assert.Equal("<p>مرحباً</p>", message.HtmlBody);
        Assert.Equal("مرحباً", message.TextBody?.Trim());
        Assert.StartsWith(envelope.MessageId.ToString("N"), message.MessageId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_recipients_are_rejected_as_message_failures()
    {
        var sender = CreateSender();
        var envelope = new EmailEnvelope(Guid.NewGuid(), "not an address <<", null, "s", "<p>b</p>", "b", "en");

        var exception = await Assert.ThrowsAsync<EmailTransportException>(() => sender.SendAsync(envelope, TestContext.Current.CancellationToken));

        Assert.Equal(EmailFailureKind.MessageRejected, exception.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private PickupDirectoryEmailSender CreateSender() =>
        new(Options.Create(new EmailOptions
        {
            Transport = EmailTransport.PickupDirectory,
            FromAddress = "no-reply@example.com",
            PickupDirectory = _directory,
        }));
}
