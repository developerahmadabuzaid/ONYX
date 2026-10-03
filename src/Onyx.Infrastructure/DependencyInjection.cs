using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Email;
using Onyx.Application.Notifications;
using Onyx.Application.Providers;
using Onyx.Domain.Common;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;
using Onyx.Infrastructure.Email.Templates;
using Onyx.Infrastructure.Persistence;
using Onyx.Infrastructure.Persistence.Interceptors;
using Onyx.Infrastructure.Persistence.Repositories;

namespace Onyx.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence and infrastructure services shared by Onyx.API and Onyx.Worker. Hosts must register their own
    /// <see cref="Application.Abstractions.ICurrentUser"/> and <see cref="Application.Abstractions.IRequestContext"/>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .Configure(options => options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        // Order matters: stamp fields / convert soft deletes → audit the result → capture domain events.
        services.AddScoped<AuditFieldsInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<DomainEventOutboxInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(database.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable(ApplicationDbContext.MigrationsHistoryTable);
                npgsql.CommandTimeout(database.CommandTimeoutSeconds);
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditFieldsInterceptor>(),
                serviceProvider.GetRequiredService<AuditLogInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>());
        });
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IProviderConnectionRepository, ProviderConnectionRepository>();

        services.AddSingleton<IEmailTemplate, ProviderTestEmailTemplate>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();

        services.Configure<DomainEventRegistryOptions>(options => options.Assemblies.Add(typeof(IDomainEvent).Assembly));
        services.AddSingleton<DomainEventTypeRegistry>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
