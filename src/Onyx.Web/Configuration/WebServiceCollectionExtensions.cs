using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using Onyx.Web.ApiClients;
using Onyx.Web.ApiClients.Handlers;
using Onyx.Web.Authentication;
using Onyx.Web.Consent;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Configuration;

public static class WebServiceCollectionExtensions
{
    public static IServiceCollection AddOnyxWeb(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        AddValidatedOptions(services, configuration);
        services.AddHttpContextAccessor();
        services.TryAddSingletonTimeProvider();

        var keysDirectory = configuration[$"{DataProtectionSettings.SectionName}:{nameof(DataProtectionSettings.KeysDirectory)}"];
        var dataProtection = services.AddDataProtection().SetApplicationName("Onyx.Web");
        if (!string.IsNullOrWhiteSpace(keysDirectory))
        {
            // Relative paths are anchored to the content root, never to whatever directory the process was started from.
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keysDirectory, environment.ContentRootPath)));
        }

        services.AddSingleton<LanguageCatalog>();
        services.AddSingleton<LanguageSuggestion>();
        services.AddSingleton(_ =>
        {
            var routes = new LocalizedRouteTable();
            foreach (var route in Leads.LeadPages.Routes)
            {
                routes.Register(route);
            }

            routes.Register(Search.SearchPages.Route);
            routes.Register(News.NewsPages.Route);
            foreach (var route in Customers.CustomerPages.Routes)
            {
                routes.Register(route);
            }

            return routes;
        });
        services.AddSingleton(Content.SitePages.SitePageCatalog.LoadEmbedded());
        services.AddOptions<Content.SiteContent.SiteContentOptions>().Bind(configuration.GetSection(Content.SiteContent.SiteContentOptions.SectionName));
        services.AddSingleton<Content.SiteContent.SiteContentProvider>();
        services.AddScoped<Content.SiteContent.SiteContentContext>();
        services.AddScoped<Content.SiteContent.SiteCopy>();
        services.AddScoped<Content.SiteContent.EffectiveSitePages>();
        services.AddSingleton<LocalizedRouteTransformer>();
        services.AddScoped<SeoContext>();
        services.AddScoped<ConsentState>();
        services.Configure<RouteOptions>(options => options.ConstraintMap[LanguageRouteConstraint.Name] = typeof(LanguageRouteConstraint));

        // Emit Arabic (and all other) text as characters rather than &#x...; entities; HTML-special characters are still encoded.
        services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

        services.AddLocalization(options => options.ResourcesPath = string.Empty);
        services.AddOptions<RequestLocalizationOptions>().Configure<LanguageCatalog>((options, languages) =>
        {
            var cultures = languages.Languages.Select(language => new System.Globalization.CultureInfo(language.Code)).ToList();
            options.DefaultRequestCulture = new RequestCulture(languages.DefaultLanguage);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders = [new OnyxRequestCultureProvider(languages)];
        });

        services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                options.Filters.Add<AdminSessionEndedFilter>();
                options.Filters.Add<Customers.CustomerSessionEndedFilter>();
                options.Filters.Add<ApiUnavailableFilter>();
                options.Filters.Add<Analytics.AnalyticsTagFilter>();
            })
            .AddViewLocalization()
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
        services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options => options.ViewLocationExpanders.Add(new Areas.Admin.Delivery.DeliveryViewLocationExpander()));

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-onyx.af";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.HeaderName = null;
        });

        // TempData carries one-time tokens and sign-in challenges between requests: encrypted, HttpOnly, never in the page.
        services.Configure<CookieTempDataProviderOptions>(options =>
        {
            options.Cookie.Name = "__Host-onyx.tmp";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        AddBffAuthentication(services);
        AddApiClients(services);

        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in settings.Value.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in settings.Value.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return services;
    }

    private static void AddValidatedOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OnyxApiOptions>().Bind(configuration.GetSection(OnyxApiOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SiteOptions>().Bind(configuration.GetSection(SiteOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ConsentOptions>().Bind(configuration.GetSection(ConsentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TurnstileOptions>().Bind(configuration.GetSection(TurnstileOptions.SectionName)).ValidateDataAnnotations()
            .Validate<IHostEnvironment>(
                (turnstile, environment) => !environment.IsProduction() || turnstile.IsConfigured,
                "Production requires Turnstile:SiteKey (the public site key of the Cloudflare Turnstile widget).")
            .ValidateOnStart();
        services.AddOptions<AnalyticsOptions>().Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .Validate(options => AnalyticsOptions.IsValidMeasurementId(options.Ga4MeasurementId), "Analytics:Ga4MeasurementId must look like G-XXXXXXXX (or be empty).")
            .ValidateOnStart();
        services.AddOptions<DataProtectionSettings>().Bind(configuration.GetSection(DataProtectionSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<WebLocalizationOptions>().Bind(configuration.GetSection(WebLocalizationOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ForwardedHeadersSettings>()
            .Bind(configuration.GetSection(ForwardedHeadersSettings.SectionName))
            .Validate(
                settings => settings.KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _))
                    && settings.KnownNetworks.All(network => System.Net.IPNetwork.TryParse(network, out _)),
                "ForwardedHeaders entries must be valid IP addresses / CIDR networks.")
            .ValidateOnStart();

        services.AddSingleton<WebOptionsValidator>();
        services.AddSingleton<IValidateOptions<OnyxApiOptions>>(provider => provider.GetRequiredService<WebOptionsValidator>());
        services.AddSingleton<IValidateOptions<SiteOptions>>(provider => provider.GetRequiredService<WebOptionsValidator>());
        services.AddSingleton<IValidateOptions<WebLocalizationOptions>>(provider => provider.GetRequiredService<WebOptionsValidator>());
    }

    private static void AddBffAuthentication(IServiceCollection services)
    {
        services.AddSingleton<SessionRefreshCoordinator>();
        services.AddScoped<BffCookieEvents>();
        services.AddScoped<AdminSignInService>();
        services.AddScoped<CurrentAdminContext>();

        services.AddAuthentication(BffDefaults.Scheme)
            .AddCookie(BffDefaults.Scheme, options =>
            {
                options.Cookie.Name = BffDefaults.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;
                options.SlidingExpiration = false;
                options.LoginPath = "/admin/account/login";
                options.LogoutPath = "/admin/account/logout";
                options.AccessDeniedPath = "/admin/account/access-denied";
                options.ReturnUrlParameter = "returnUrl";
                options.EventsType = typeof(BffCookieEvents);
            })
            // Customers (docs/architecture.md §24.8): a separate scheme and cookie. Admin pages authenticate only the staff scheme
            // (the default); customer pages declare this one, so neither sign-in is ever accepted in the other area.
            .AddCookie(Customers.CustomerWebAuth.Scheme, options =>
            {
                options.Cookie.Name = Customers.CustomerWebAuth.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                // Lax so the first visit from an emailed link is already signed in; every POST is still antiforgery-protected.
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;
                options.SlidingExpiration = false;
                options.ReturnUrlParameter = Customers.CustomerPages.ReturnUrlParameter;
                options.EventsType = typeof(Customers.CustomerCookieEvents);
            });

        services.AddScoped<Customers.CustomerCookieEvents>();
        services.AddAuthorization();
    }

    private static void AddApiClients(IServiceCollection services)
    {
        services.AddTransient<RequestContextHandler>();
        services.AddTransient<AccessTokenHandler>();
        services.AddTransient<Customers.CustomerSessionHandler>();

        Configure(services.AddHttpClient<IAuthApiClient, AuthApiClient>());
        Configure(services.AddHttpClient<IConsentApiClient, ConsentApiClient>());
        Configure(services.AddHttpClient<IAdminApiClient, AdminApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminCmsApiClient, AdminCmsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IPublicContentApiClient, PublicContentApiClient>(), publicRead: true);
        Configure(services.AddHttpClient<ISearchApiClient, SearchApiClient>(), publicRead: true);
        Configure(services.AddHttpClient<IPublicNewsApiClient, PublicNewsApiClient>(), publicRead: true);
        Configure(services.AddHttpClient<IPublicSiteContentApiClient, PublicSiteContentApiClient>(), publicRead: true);
        Configure(services.AddHttpClient<IAdminSiteContentApiClient, AdminSiteContentApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminNewsApiClient, AdminNewsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminDeliveryApiClient, AdminDeliveryApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminAnalyticsApiClient, AdminAnalyticsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<ILeadsApiClient, LeadsApiClient>());
        Configure(services.AddHttpClient<IAdminLeadsApiClient, AdminLeadsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminPrivacyApiClient, AdminPrivacyApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminStaffApiClient, AdminStaffApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminCommerceApiClient, AdminCommerceApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<ICustomerAuthApiClient, CustomerAuthApiClient>());
        Configure(services.AddHttpClient<ICustomerApiClient, CustomerApiClient>().AddHttpMessageHandler<Customers.CustomerSessionHandler>());
        Configure(services.AddHttpClient<IProductsApiClient, ProductsApiClient>(), publicRead: true);

        static void Configure(IHttpClientBuilder builder, bool publicRead = false)
        {
            builder.ConfigureHttpClient((provider, client) =>
            {
                var api = provider.GetRequiredService<IOptions<OnyxApiOptions>>().Value;
                client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            });
            builder.AddHttpMessageHandler<RequestContextHandler>();

            // Never retry POSTs: a retried refresh would look like token reuse and end the session.
            builder.AddStandardResilienceHandler(options =>
            {
                options.Retry.DisableForUnsafeHttpMethods();
                if (publicRead)
                {
                    // Visitor page views: one quick retry, then render without the optional data (or the service page) instead
                    // of holding the visitor through the default exponential back-off (seconds per call).
                    options.Retry.MaxRetryAttempts = 1;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                    options.Retry.BackoffType = Polly.DelayBackoffType.Constant;
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(7);

                    // While the API is failing, stop waiting on it: after a handful of failures the circuit opens for a short
                    // break and calls fail immediately (rendered as "unavailable"), then it probes again.
                    options.CircuitBreaker.MinimumThroughput = 4;
                    options.CircuitBreaker.FailureRatio = 0.5;
                    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                }
            });
        }
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
