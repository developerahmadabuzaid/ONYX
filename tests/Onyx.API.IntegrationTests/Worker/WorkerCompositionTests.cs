using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Persistence;
using Onyx.Application.Email;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Builds the real Worker composition with scope/build validation; no database access needed.</summary>
public sealed class WorkerCompositionTests
{
    private static readonly ManualTimeProvider Time = new(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Worker_composition_resolves_all_hosted_services_and_processors()
    {
        await using var host = WorkerTestHost.Create(OnyxApiFactory.UnreachableDatabase, "composition", Time);

        var hosted = host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        Assert.Contains(typeof(OutboxDispatcherService), hosted);
        Assert.Contains(typeof(EmailDispatcherService), hosted);
        Assert.Contains(typeof(JobSchedulerService), hosted);
        Assert.IsType<UnconfiguredEmailSender>(host.Get<IEmailSender>());
    }

    [Theory]
    [InlineData("Smtp", typeof(SmtpEmailSender))]
    [InlineData("PickupDirectory", typeof(PickupDirectoryEmailSender))]
    public async Task Transport_setting_selects_the_sender(string transport, Type expected)
    {
        await using var host = WorkerTestHost.Create(
            OnyxApiFactory.UnreachableDatabase, "composition", Time, settings: new Dictionary<string, string?>
            {
                ["Email:Transport"] = transport,
                ["Email:FromAddress"] = "no-reply@example.com",
                ["Email:Smtp:Host"] = "smtp.example.com",
                ["Email:PickupDirectory"] = Path.GetTempPath(),
            },
            environmentName: "Development");

        Assert.IsType(expected, host.Get<IEmailSender>());
    }

    [Theory]
    [InlineData("Production", "PickupDirectory", "smtp.example.com", "StartTls", "only allowed in Development")]
    [InlineData("Production", "Smtp", "smtp.example.com", "None", "Plaintext SMTP")]
    [InlineData("Production", "Smtp", "", "StartTls", "Email:Smtp:Host is required")]
    public async Task Unsafe_or_incomplete_email_configuration_fails_validation(
        string environment, string transport, string host, string security, string expectedMessage)
    {
        await using var testHost = WorkerTestHost.Create(
            OnyxApiFactory.UnreachableDatabase, "composition", Time, settings: new Dictionary<string, string?>
            {
                ["Email:Transport"] = transport,
                ["Email:FromAddress"] = "no-reply@example.com",
                ["Email:Smtp:Host"] = host,
                ["Email:Smtp:Security"] = security,
                ["Email:PickupDirectory"] = Path.GetTempPath(),
            },
            environmentName: environment);

        var exception = Assert.Throws<OptionsValidationException>(() => testHost.Get<IOptions<EmailOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.Contains(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Smtp_credentials_must_be_set_together()
    {
        await using var host = WorkerTestHost.Create(
            OnyxApiFactory.UnreachableDatabase, "composition", Time, settings: new Dictionary<string, string?>
            {
                ["Email:Transport"] = "Smtp",
                ["Email:FromAddress"] = "no-reply@example.com",
                ["Email:Smtp:Host"] = "smtp.example.com",
                ["Email:Smtp:Username"] = "mailer",
            });

        var exception = Assert.Throws<OptionsValidationException>(() => host.Get<IOptions<EmailOptions>>().Value);
        Assert.Contains(exception.Failures, failure => failure.Contains("must be set together", StringComparison.Ordinal));
    }
}
