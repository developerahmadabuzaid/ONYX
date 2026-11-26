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
            .Validate(
                options => options.RetentionBatchSize is >= 1 and <= 10_000 && options.RetentionMaxBatchesPerRun is >= 1 and <= 10_000,
                "Privacy:RetentionBatchSize and Privacy:RetentionMaxBatchesPerRun must be between 1 and 10000.")
            .ValidateOnStart();
        services.AddSingleton<Application.Privacy.IIpAddressHasher, Privacy.HmacIpAddressHasher>();
        services.AddScoped<Application.Privacy.ICookieConsentRepository, Privacy.CookieConsentRepository>();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<Privacy.PrivacyOptions>>().Value;
            return new Application.Privacy.RetentionSettings(options.RetentionBatchSize, options.RetentionMaxBatchesPerRun);
        });
        services.AddScoped<Application.Privacy.IDataSubjectRequestRepository, DataSubjectRequestRepository>();
        services.AddScoped<Application.Privacy.IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<Application.Privacy.IPersonalDataStore, Privacy.PersonalDataStore>();
        services.AddScoped<Application.Search.ISearchDocumentRepository, Search.SearchDocumentRepository>();
        services.AddScoped<Application.Search.ISiteSearchService, Search.PostgresSiteSearchService>();

        services.AddSingleton<IEmailTemplate, ProviderTestEmailTemplate>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddSingleton<IEmailTemplateCatalog, EmailTemplateCatalog>();
        services.AddScoped<IEmailTemplateCopyRepository, EmailTemplateCopyRepository>();
        services.AddScoped<IEmailOutbox, EmailOutbox>();

        AddContent(services, configuration);
        AddLeads(services, configuration);
        AddNews(services, configuration);
        AddAnalytics(services, configuration);
        AddCommerce(services, configuration);

        services.Configure<DomainEventRegistryOptions>(options => options.Assemblies.Add(typeof(IDomainEvent).Assembly));
        services.AddSingleton<DomainEventTypeRegistry>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Production requires Turnstile (owner decision after Stage 8): the host refuses to start without a secret and a hostname.
    /// Called only by Onyx.API, the host that verifies tokens; the Worker never does.
    /// </summary>
    public static IServiceCollection RequireTurnstileInProduction(this IServiceCollection services)
    {
        services.AddOptions<Security.TurnstileOptions>()
            .Validate<Microsoft.Extensions.Hosting.IHostEnvironment, IOptions<Seo.PublicSiteOptions>, IOptions<IdentitySecurityOptions>>(
                (turnstile, environment, site, identity) =>
                    turnstile.IsValidFor(Microsoft.Extensions.Hosting.HostEnvironmentEnvExtensions.IsProduction(environment), site.Value.PublicBaseUrl, identity.Value.AdminPortalBaseUrl),
                "Production requires Turnstile: set Turnstile:SecretKey (secret store) and a hostname (Turnstile:AllowedHostnames, Site:PublicBaseUrl or IdentitySecurity:AdminPortalBaseUrl).")
            .ValidateOnStart();
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

    /// <summary>
    /// Stage 8: inquiries, leads, booking, their email templates, the processed-event log and Turnstile verification
    /// (fails closed until a secret is configured).
    /// </summary>
    private static void AddLeads(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Leads.LeadsOptions>().Bind(configuration.GetSection(Leads.LeadsOptions.SectionName)).ValidateDataAnnotations()
            .Validate(
                options => options.DefaultRecipients.Concat(options.RecipientsByType.Values.SelectMany(list => list)).All(Leads.LeadsOptions.IsValidAddress),
                "Leads recipients must be valid email addresses.")
            .Validate(
                options => options.RecipientsByType.Keys.All(key => Enum.TryParse<Domain.Leads.InquiryType>(key, ignoreCase: false, out _)),
                "Leads:RecipientsByType keys must be inquiry type names.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<Leads.LeadsOptions>>().Value;
            var site = provider.GetRequiredService<IOptions<Seo.PublicSiteOptions>>().Value;
            var adminBase = configuration["IdentitySecurity:AdminPortalBaseUrl"];
            return new Application.Leads.LeadSettings(
                options.ConsentVersion,
                options.DefaultRecipients.ToList(),
                options.RecipientsByType.ToDictionary(
                    entry => Enum.Parse<Domain.Leads.InquiryType>(entry.Key), entry => (IReadOnlyList<string>)entry.Value.ToList()),
                Uri.TryCreate(adminBase, UriKind.Absolute, out var admin) && (admin.Scheme == Uri.UriSchemeHttps || admin.IsLoopback) ? new Uri(admin.GetLeftPart(UriPartial.Authority)) : null,
                site.Origin,
                TimeSpan.FromHours(options.ManageLinkHoursAfterSlot));
        });

        services.AddScoped<Application.Leads.IInquiryRepository, InquiryRepository>();
        services.AddScoped<Application.Leads.ILeadRepository, LeadRepository>();
        services.AddScoped<Application.Booking.IBookingRepository, BookingRepository>();
        services.AddScoped<Application.Booking.IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<Application.Abstractions.Messaging.IProcessedEventLog, Persistence.Outbox.ProcessedEventLog>();

        services.AddSingleton<IEmailTemplate, InquiryReceivedEmailTemplate>();
        services.AddSingleton<IEmailTemplate, InquiryInternalEmailTemplate>();
        services.AddSingleton<IEmailTemplate, BookingConfirmedEmailTemplate>();
        services.AddSingleton<IEmailTemplate, BookingCancelledEmailTemplate>();
        services.AddSingleton<IEmailTemplate, BookingInternalEmailTemplate>();

        services.AddOptions<Security.TurnstileOptions>().Bind(configuration.GetSection(Security.TurnstileOptions.SectionName)).ValidateDataAnnotations()
            .Validate(
                options => Uri.TryCreate(options.VerifyEndpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == Uri.UriSchemeHttps,
                "Turnstile:VerifyEndpoint must be an absolute https URL.")
            .ValidateOnStart();
        services.AddHttpClient<Application.Abstractions.ICaptchaVerifier, Security.TurnstileCaptchaVerifier>((provider, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<Security.TurnstileOptions>>().Value.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OnyxVanguard-Turnstile/1.0");
        });
    }

    /// <summary>
    /// SaaS platform (docs/architecture.md §24.8): isolated customer identity, product catalogue, subscriptions, manual invoices
    /// and the AI demo queue. The customer templates are rendered by the Worker like every other email.
    /// </summary>
    private static void AddCommerce(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Application.Customers.CustomerAccountOptions>()
            .Bind(configuration.GetSection(Application.Customers.CustomerAccountOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<Application.Customers.CustomerAccountOptions>>().Value);
        services.AddOptions<Application.Products.AiDemoOptions>()
            .Bind(configuration.GetSection(Application.Products.AiDemoOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<Application.Products.AiDemoOptions>>().Value);

        services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<Domain.Customers.CustomerAccount>,
            Microsoft.AspNetCore.Identity.PasswordHasher<Domain.Customers.CustomerAccount>>();
        services.TryAddScoped<Application.Customers.ICurrentCustomer, Customers.NoCurrentCustomer>();
        services.AddScoped<Application.Customers.ICustomerAuthService, Customers.CustomerAuthService>();
        services.AddScoped<Application.Customers.ICustomerAccountRepository, CustomerAccountRepository>();
        services.AddScoped<Application.Products.ISaasProductRepository, SaasProductRepository>();
        services.AddScoped<Application.Products.IAiDemoRunRepository, AiDemoRunRepository>();
        services.AddScoped<Application.Subscriptions.ICustomerSubscriptionRepository, CustomerSubscriptionRepository>();
        services.AddScoped<Application.Billing.IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<Application.Commerce.ICommerceReadModel, CommerceReadModel>();

        services.AddSingleton<IEmailTemplate, CustomerVerifyEmailTemplate>();
        services.AddSingleton<IEmailTemplate, CustomerAlreadyRegisteredEmailTemplate>();
        services.AddSingleton<IEmailTemplate, CustomerPasswordResetEmailTemplate>();
        services.AddSingleton<IEmailTemplate, CustomerSubscriptionUpdatedEmailTemplate>();
    }

    /// <summary>
    /// Stage 11: News Hub repositories, the SSRF-safe outbound client (every admin-supplied URL goes through it), the feed reader
    /// and the news summary boundary. The summary service is the not-configured placeholder until Onyx's external AI Agent is
    /// integrated by registering an adapter for <see cref="Application.News.INewsSummaryService"/>; items stay held until then.
    /// </summary>
    private static void AddNews(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<News.NewsOptions>().Bind(configuration.GetSection(News.NewsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<News.NewsOptions>>().Value;
            return new Application.News.NewsSettings(
                options.MaxSourcesPerRun, options.MaxEntriesPerSource, options.MaxSummariesPerRun, TimeSpan.FromSeconds(options.SummaryTimeoutSeconds),
                TimeSpan.FromMinutes(options.RunBudgetMinutes), TimeSpan.FromHours(options.SimilarWindowHours));
        });

        services.AddOptions<Outbound.SafeOutboundHttpOptions>().Bind(configuration.GetSection(Outbound.SafeOutboundHttpOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.TryAddSingleton<Outbound.IOutboundDnsResolver, Outbound.SystemDnsResolver>();
        services.TryAddSingleton<Outbound.IOutboundConnector, Outbound.SocketConnector>();
        services.AddSingleton<Outbound.SafeOutboundHttpClient>();
        services.AddSingleton<Outbound.RobotsTxtPolicy>();

        services.AddScoped<Application.News.INewsSourceRepository, NewsSourceRepository>();
        services.AddScoped<Application.News.INewsCategoryRepository, NewsCategoryRepository>();
        services.AddScoped<Application.News.INewsItemRepository, NewsItemRepository>();
        services.AddScoped<Application.Delivery.IDeliveryProjectRepository, DeliveryProjectRepository>();
        services.AddScoped<Application.Delivery.IIssueRepository, IssueRepository>();
        services.AddScoped<Application.Delivery.ISprintRepository, SprintRepository>();
        services.AddScoped<Application.Delivery.IDeliveryReadModel, DeliveryReadModel>();
        services.AddScoped<Application.Delivery.ISavedIssueFilterRepository, SavedIssueFilterRepository>();
        services.AddScoped<Application.News.INewsFeedReader, News.SyndicationNewsFeedReader>();
        services.TryAddScoped<Application.News.INewsSummaryService, News.NotConfiguredNewsSummaryService>();
    }

    /// <summary>
    /// Stage 12: analytics. The Google adapters (GA4 Data API, Search Console; read-only scopes) are the only implementations;
    /// they report "not configured" until the owner supplies the property, site and service-account key (secret store). No fake
    /// or sample provider is registered here: tests register their own.
    /// </summary>
    private static void AddAnalytics(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Analytics.GoogleAnalyticsOptions>().Bind(configuration.GetSection(Analytics.GoogleAnalyticsOptions.SectionName))
            .Validate(options => options.IsWellFormed(), "Analytics:Google ids must be well-formed (digits for Ga4PropertyId; sc-domain:host or an https URL ending in '/' for SearchConsoleSiteUrl).")
            .ValidateOnStart();
        services.AddOptions<Analytics.AnalyticsSyncOptions>().Bind(configuration.GetSection(Analytics.AnalyticsSyncOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<Analytics.AnalyticsSyncOptions>>().Value;
            return new Application.Analytics.AnalyticsSettings(
                options.InitialDays, options.RefreshDays, options.MaxDaysPerRun, options.MaxRowsPerReport, options.MaxRowsPerDay, options.WebAnalyticsDelayDays,
                options.SearchDelayDays, TimeSpan.FromSeconds(options.FetchTimeoutSeconds), TimeSpan.FromHours(options.StaleAfterHours));
        });

        services.AddSingleton<Analytics.GoogleServiceAccount>();
        services.TryAddSingleton<Application.Analytics.IWebAnalyticsProvider, Analytics.GoogleAnalyticsDataProvider>();
        services.TryAddSingleton<Application.Analytics.ISearchPerformanceProvider, Analytics.GoogleSearchConsoleProvider>();
        services.AddScoped<Application.Analytics.IAnalyticsMetricRepository, AnalyticsMetricRepository>();
        services.AddScoped<Application.Analytics.IAnalyticsSyncStateRepository, AnalyticsSyncStateRepository>();
        services.AddScoped<Application.Analytics.IAnalyticsFunnelRepository, AnalyticsFunnelRepository>();
        services.AddScoped<Application.Jobs.IJobTrigger, BackgroundJobs.JobTrigger>();
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
        services.AddScoped<Application.SiteContent.ISiteContentRepository, SiteContentRepository>();
        services.AddScoped<Application.Knowledge.IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<Application.Knowledge.IInformationRequestRepository, InformationRequestRepository>();
        services.AddScoped<Application.Intelligence.IKeywordTargetRepository, KeywordTargetRepository>();
        services.AddScoped<Application.Intelligence.ICompetitorRepository, CompetitorRepository>();
        services.AddScoped<Application.Intelligence.ISeoRecommendationRepository, SeoRecommendationRepository>();
        services.AddScoped<Application.Intelligence.IContentCalendarRepository, ContentCalendarRepository>();
        services.AddScoped<Application.Intelligence.IContentPerformanceRepository, ContentPerformanceRepository>();
        services.AddScoped<Application.Proof.IFeaturedProjectRepository, FeaturedProjectRepository>();
        services.AddScoped<Application.Proof.IProofItemRepository, ProofItemRepository>();
        services.AddScoped<Application.Insights.IAuthorRepository, AuthorRepository>();

        AddSeo(services, configuration);
    }
}
