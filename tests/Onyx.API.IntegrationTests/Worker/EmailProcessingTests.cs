using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Email;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class EmailProcessingTests(PostgresFixture fixture) : WorkerDatabaseTestBase(fixture)
{
    private readonly FakeEmailSender _sender = new();

    [Fact]
    public async Task Queued_emails_are_rendered_in_the_submission_language_and_persist_with_the_unit_of_work()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");

        var result = await QueueAsync(host, new EmailRequest(ProviderTestEmailTemplate.TemplateKey, "ar", "client@example.com", "عميل", new Dictionary<string, string>()));

        Assert.True(result.IsSuccess);
        var message = Assert.Single(await ReadAsync(db => db.EmailMessages));
        Assert.Equal(EmailMessageStatus.Pending, message.Status);
        Assert.Equal("ar", message.LanguageCode);
        Assert.Contains("lang=\"ar\" dir=\"rtl\"", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("text-align:right", message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("رسالة اختبار من Onyx Vanguard Solutions", message.Subject);
        Assert.Contains("إعدادات البريد الإلكتروني تعمل", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_values_are_html_encoded()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");
        var hostile = "<script>alert('x')</script>";

        await QueueAsync(host, new EmailRequest(TestModelEmailTemplate.TemplateKey, "en", "client@example.com", null, new Dictionary<string, string> { ["name"] = hostile }));

        var message = Assert.Single(await ReadAsync(db => db.EmailMessages));
        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"ltr\"", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-template", "en", "client@example.com", "Email.UnknownTemplate")]
    [InlineData(ProviderTestEmailTemplate.TemplateKey, "fr", "client@example.com", "Email.UnsupportedLanguage")]
    [InlineData(ProviderTestEmailTemplate.TemplateKey, "en", "not-an-address", "Email.Invalid")]
    public async Task Invalid_requests_are_rejected_and_nothing_is_queued(string template, string language, string address, string expectedCode)
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");

        var result = await QueueAsync(host, new EmailRequest(template, language, address, null, new Dictionary<string, string>()));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Empty(await ReadAsync(db => db.EmailMessages));
    }

    [Fact]
    public async Task Unsafe_links_are_rejected()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");

        var result = await QueueAsync(host, new EmailRequest(
            TestModelEmailTemplate.TemplateKey, "en", "client@example.com", null,
            new Dictionary<string, string> { ["name"] = "Client", ["link"] = "javascript:alert(1)" }));

        Assert.True(result.IsFailure);
        Assert.Equal("Email.UnsafeLink", result.Error!.Code);
    }

    [Fact]
    public async Task Successful_sends_mark_messages_sent_and_the_provider_active()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");
        await QueueTestEmailsAsync(host, 3);

        Assert.Equal(3, await host.Get<EmailProcessor>().ProcessBatchAsync(Token));

        Assert.Equal(3, _sender.Sent.Count);
        Assert.All(await ReadAsync(db => db.EmailMessages), message =>
        {
            Assert.Equal(EmailMessageStatus.Sent, message.Status);
            Assert.Equal(Start, message.SentAtUtc);
            Assert.Null(message.LockedBy);
        });
        var connection = Assert.Single(await ReadAsync(db => db.ProviderConnections));
        Assert.Equal(FakeEmailSender.Key, connection.ProviderKey);
        Assert.Equal(ProviderConnectionStatus.Active, connection.Status);
    }

    [Fact]
    public async Task Transport_outage_postpones_the_batch_without_consuming_attempts_and_raises_a_provider_failure()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");
        await QueueTestEmailsAsync(host, 3);
        _sender.Behaviour = _ => throw new EmailTransportException(EmailFailureKind.ProviderUnavailable, "SMTP server unreachable or refused the connection.");

        await host.Get<EmailProcessor>().ProcessBatchAsync(Token);

        Assert.All(await ReadAsync(db => db.EmailMessages), message =>
        {
            Assert.Equal(EmailMessageStatus.Pending, message.Status);
            Assert.Equal(0, message.AttemptCount);
            Assert.Equal(Start.AddSeconds(30), message.NextAttemptAtUtc);
            Assert.Null(message.LockedBy);
        });

        var connection = Assert.Single(await ReadAsync(db => db.ProviderConnections));
        Assert.Equal(ProviderConnectionStatus.Error, connection.Status);
        Assert.Contains(await ReadAsync(db => db.OutboxMessages), message => message.Type == typeof(ProviderConnectionFailed).FullName);

        // The emails survive the outage and go out once the transport recovers.
        _sender.Behaviour = _ => Task.CompletedTask;
        Time.Advance(TimeSpan.FromSeconds(31));
        await host.Get<EmailProcessor>().ProcessBatchAsync(Token);
        Assert.All(await ReadAsync(db => db.EmailMessages), message => Assert.Equal(EmailMessageStatus.Sent, message.Status));
    }

    [Fact]
    public async Task Repeatedly_rejected_messages_are_dead_lettered_with_a_notification()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost("worker-a");
        await QueueTestEmailsAsync(host, 1);
        _sender.Behaviour = _ => throw new EmailTransportException(EmailFailureKind.MessageRejected, "Recipient rejected by the SMTP server (550).");
        var processor = host.Get<EmailProcessor>();

        await processor.ProcessBatchAsync(Token);
        Time.Advance(TimeSpan.FromMinutes(2));
        await processor.ProcessBatchAsync(Token);
        Time.Advance(TimeSpan.FromMinutes(5));
        await processor.ProcessBatchAsync(Token);

        var message = Assert.Single(await ReadAsync(db => db.EmailMessages));
        Assert.Equal(EmailMessageStatus.DeadLettered, message.Status);
        Assert.Equal(3, message.AttemptCount);
        Assert.Equal("Recipient rejected by the SMTP server (550).", message.LastError);

        var notification = Assert.Single(await ReadAsync(db => db.AdminNotifications));
        Assert.Equal(NotificationType.EmailDeliveryFailed, notification.Type);
        Assert.Equal(Permissions.SystemJobs.View, notification.RequiredPermission);
        Assert.Equal(ProviderTestEmailTemplate.TemplateKey, notification.Parameters["template"]);
    }

    [Fact]
    public async Task Unconfigured_transport_leaves_messages_queued_and_marks_the_provider_not_configured()
    {
        Fixture.RequireDatabase();
        _sender.IsConfigured = false;
        await using var host = CreateHost("worker-a");
        await QueueTestEmailsAsync(host, 2);

        Assert.Equal(0, await host.Get<EmailProcessor>().ProcessBatchAsync(Token));

        Assert.Empty(_sender.Sent);
        Assert.All(await ReadAsync(db => db.EmailMessages), message => Assert.Equal(EmailMessageStatus.Pending, message.Status));
        var connection = Assert.Single(await ReadAsync(db => db.ProviderConnections));
        Assert.Equal(ProviderConnectionStatus.NotConfigured, connection.Status);
    }

    [Fact]
    public async Task Two_workers_send_every_email_exactly_once()
    {
        Fixture.RequireDatabase();
        await using var first = CreateHost("worker-a", batchSize: 4);
        await using var second = CreateHost("worker-b", batchSize: 4);
        await QueueTestEmailsAsync(first, 25);

        await Task.WhenAll(DrainAsync(first.Get<EmailProcessor>()), DrainAsync(second.Get<EmailProcessor>()));

        Assert.Equal(25, _sender.Sent.Count);
        Assert.Equal(25, _sender.Sent.Select(envelope => envelope.MessageId).Distinct().Count());
    }

    private WorkerTestHost CreateHost(string instanceId, int batchSize = 20) =>
        WorkerTestHost.Create(
            ConnectionString,
            instanceId,
            Time,
            services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_sender);
                services.AddSingleton<IEmailTemplate, TestModelEmailTemplate>();
            },
            new Dictionary<string, string?> { ["Worker:EmailBatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    private static Task<Result<Guid>> QueueAsync(WorkerTestHost host, EmailRequest request) =>
        host.InScopeAsync(async services =>
        {
            var result = await services.GetRequiredService<IEmailOutbox>().QueueAsync(request, Token);
            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Token);
            return result;
        });

    private static async Task QueueTestEmailsAsync(WorkerTestHost host, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var result = await QueueAsync(host, new EmailRequest(
                ProviderTestEmailTemplate.TemplateKey, i % 2 == 0 ? "ar" : "en", $"client{i}@example.com", null, new Dictionary<string, string>()));
            Assert.True(result.IsSuccess);
        }
    }

    private static async Task DrainAsync(EmailProcessor processor)
    {
        while (await processor.ProcessBatchAsync(Token) > 0)
        {
        }
    }
}
