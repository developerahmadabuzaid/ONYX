using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Identity;
using Onyx.Infrastructure.Auditing;
using Onyx.Infrastructure.Identity;
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

        services.AddMemoryCache();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IAuditLogQueries, AuditLogQueries>();

        services.AddOptions<Privacy.PrivacyOptions>()
            .Bind(configuration.GetSection(Privacy.PrivacyOptions.SectionName))
            .Validate(options => Privacy.PrivacyOptions.IsValidKey(options.IpHashKey), "Privacy:IpHashKey, when set, must be base64 of at least 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton<Application.Privacy.IIpAddressHasher, Privacy.HmacIpAddressHasher>();
        services.AddScoped<Application.Privacy.ICookieConsentRepository, Privacy.CookieConsentRepository>();

        services.AddSingleton<IEmailTemplate, ProviderTestEmailTemplate>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddSingleton<IEmailTemplateCatalog, EmailTemplateCatalog>();
        services.AddScoped<IEmailTemplateCopyRepository, EmailTemplateCopyRepository>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();

        AddContent(services, configuration);

        services.Configure<DomainEventRegistryOptions>(options => options.Assemblies.Add(typeof(IDomainEvent).Assembly));
        services.AddSingleton<DomainEventTypeRegistry>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>Public site settings, IndexNow change notification (inactive until configured and launched) and the Organization gate.</summary>
    private static void AddSeo(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Seo.PublicSiteOptions>().Bind(configuration.GetSection(Seo.PublicSiteOptions.SectionName))
            .Validate(options => !options.AllowIndexing || options.Origin is not null, "Site:PublicBaseUrl must be an absolute https origin when Site:AllowIndexing is true.")
            .ValidateOnStart();
        services.AddOptions<Seo.IndexNowOptions>().Bind(configuration.GetSection(Seo.IndexNowOptions.SectionName)).ValidateDataAnnotations()
            .Validate(options => string.IsNullOrEmpty(options.Key) || Seo.IndexNowNotifier.IsValidKey(options.Key), "IndexNow:Key must be 8-128 letters, digits or hyphens.")
            .ValidateOnStart();
        services.AddOptions<Seo.SeoOptions>().Bind(configuration.GetSection(Seo.SeoOptions.SectionName));

        services.AddHttpClient<Application.Seo.ISearchEngineNotifier, Seo.IndexNowNotifier>((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<Seo.IndexNowOptions>>().Value.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OnyxVanguard-IndexNow/1.0");
        });

        services.AddSingleton(provider =>
        {
            var seo = provider.GetRequiredService<IOptions<Seo.SeoOptions>>().Value;
            var site = provider.GetRequiredService<IOptions<Seo.PublicSiteOptions>>().Value;
            return new Application.Seo.OrganizationProfileSettings(seo.OrganizationStructuredDataEnabled, site.Origin?.ToString());
        });
    }

    /// <summary>Stage 6: CMS repositories, media storage and inspection, content settings.</summary>
    private static void AddContent(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Content.ContentOptions>().Bind(configuration.GetSection(Content.ContentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<Content.ContentOptions>>().Value;
            return new Application.Content.ContentSettings(TimeSpan.FromMinutes(options.PreviewTokenMinutes), options.SchedulerBatchSize);
        });

        // Storage root is validated on first use: only hosts that serve media (the API) need it configured.
        services.AddOptions<Media.MediaOptions>().Bind(configuration.GetSection(Media.MediaOptions.SectionName)).ValidateDataAnnotations();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<Media.MediaOptions>>().Value;
            return new Application.Media.MediaLimits(options.MaxUploadBytes, options.MaxWidth, options.MaxHeight, options.MaxPixels, options.MaxFrames);
        });
        services.AddSingleton<Application.Media.IImageInspector, Media.ImageInspector>();
        services.AddSingleton<Application.Media.IMediaStorage, Media.LocalFileMediaStorage>();

        services.AddScoped<Application.Content.IContentRepository, ContentRepository>();
        services.AddScoped<Application.Content.IContentRevisionRepository, ContentRevisionRepository>();
        services.AddScoped<Application.Content.IContentPathRepository, ContentPathRepository>();
        services.AddScoped<Application.Content.IRedirectRepository, RedirectRepository>();
        services.AddScoped<Application.Content.IContentPreviewTokenRepository, ContentPreviewTokenRepository>();
        services.AddScoped<Application.Content.ICallToActionRepository, CallToActionRepository>();
        services.AddScoped<Application.Content.IContentRelationRepository, ContentRelationRepository>();
        services.AddScoped<Application.Content.IContentClusterRepository, ContentClusterRepository>();
        services.AddScoped<Application.Media.IMediaRepository, MediaRepository>();
        services.AddScoped<Application.Brand.IBrandKitRepository, BrandKitRepository>();
        services.AddScoped<Application.Knowledge.IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<Application.Knowledge.IInformationRequestRepository, InformationRequestRepository>();
        services.AddScoped<Application.Intelligence.IKeywordTargetRepository, KeywordTargetRepository>();
        services.AddScoped<Application.Intelligence.ICompetitorRepository, CompetitorRepository>();
        services.AddScoped<Application.Intelligence.ISeoRecommendationRepository, SeoRecommendationRepository>();
        services.AddScoped<Application.Intelligence.IContentCalendarRepository, ContentCalendarRepository>();
        services.AddScoped<Application.Intelligence.IContentPerformanceRepository, ContentPerformanceRepository>();

        AddSeo(services, configuration);
    }
}
