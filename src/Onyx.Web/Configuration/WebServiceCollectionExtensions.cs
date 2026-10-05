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

            return routes;
        });
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
                options.Filters.Add<ApiUnavailableFilter>();
            })
            .AddViewLocalization()
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

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
        services.AddOptions<TurnstileOptions>().Bind(configuration.GetSection(TurnstileOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
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
            });

        services.AddAuthorization();
    }

    private static void AddApiClients(IServiceCollection services)
    {
        services.AddTransient<RequestContextHandler>();
        services.AddTransient<AccessTokenHandler>();

        Configure(services.AddHttpClient<IAuthApiClient, AuthApiClient>());
        Configure(services.AddHttpClient<IConsentApiClient, ConsentApiClient>());
        Configure(services.AddHttpClient<IAdminApiClient, AdminApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IAdminCmsApiClient, AdminCmsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());
        Configure(services.AddHttpClient<IPublicContentApiClient, PublicContentApiClient>());
        Configure(services.AddHttpClient<ILeadsApiClient, LeadsApiClient>());
        Configure(services.AddHttpClient<IAdminLeadsApiClient, AdminLeadsApiClient>().AddHttpMessageHandler<AccessTokenHandler>());

        static void Configure(IHttpClientBuilder builder)
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
            builder.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
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
