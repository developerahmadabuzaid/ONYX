using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Application.Email;
using Onyx.Application.Jobs;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;

namespace Onyx.Infrastructure;

public static class WorkerServiceCollectionExtensions
{
    /// <summary>Background processing for Onyx.Worker only: outbox delivery, email sending, scheduled jobs.</summary>
    public static IServiceCollection AddWorkerProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<IOptions<EmailOptions>>().Value.Transport switch
        {
            EmailTransport.Smtp => new SmtpEmailSender(provider.GetRequiredService<IOptions<EmailOptions>>()),
            EmailTransport.PickupDirectory => new PickupDirectoryEmailSender(provider.GetRequiredService<IOptions<EmailOptions>>()),
            _ => new UnconfiguredEmailSender(),
        });

        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<IRequestContext, SystemRequestContext>();

        services.AddSingleton<WorkerIdentity>();
        services.AddSingleton<FailureNotifier>();
        services.AddSingleton<OutboxProcessor>();
        services.AddSingleton<EmailProcessor>();
        services.AddSingleton<JobScheduler>();

        services.AddScoped<IScheduledJob, OperationalCleanupJob>();
        services.AddScoped<IScheduledJob, ContentSchedulingJob>();
        services.AddScoped<IScheduledJob, RetentionEnforcementJob>();
        services.AddScoped<IScheduledJob, SearchIndexReconcileJob>();
        services.AddScoped<IScheduledJob, NewsIngestionJob>();

        services.AddHostedService<OutboxDispatcherService>();
        services.AddHostedService<EmailDispatcherService>();
        services.AddHostedService<JobSchedulerService>();

        return services;
    }
}
