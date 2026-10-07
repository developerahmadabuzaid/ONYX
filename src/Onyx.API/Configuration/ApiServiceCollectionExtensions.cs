using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Onyx.API.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Onyx.API.Context;
using Onyx.API.ErrorHandling;
using Onyx.API.Middleware;
using Onyx.Application.Abstractions;
using IPNetwork = System.Net.IPNetwork;

namespace Onyx.API.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddOnyxApi(this IServiceCollection services)
    {
        AddValidatedOptions(services);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IRequestContext, HttpRequestContext>();

        services.AddControllers();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions.Remove("exception");
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            if (context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();
        services.AddHsts(_ => { });
        services.AddOptions<HstsOptions>().Configure<IOptions<ApiSecurityOptions>>((hsts, security) =>
        {
            hsts.MaxAge = TimeSpan.FromDays(security.Value.HstsMaxAgeDays);
            hsts.IncludeSubDomains = true;
        });

        AddForwardedHeaders(services);
        AddRateLimiting(services);

        // Secure default: every endpoint requires an authenticated admin unless it explicitly opts out with [AllowAnonymous].
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().AddRequirements(new AuthenticatedEndpointRequirement()).Build());
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // CORS is intentionally not registered: browsers never call the API directly (BFF), so no origin is ever allowed.
        return services;
    }

    private static void AddValidatedOptions(IServiceCollection services)
    {

        services.AddOptions<ApiSecurityOptions>()
            .BindConfiguration(ApiSecurityOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(options => IsValid(options.Global) && IsValid(options.Strict) && IsValid(options.Search), "RateLimiting windows and permit limits must be positive.")
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersSettings>()
            .BindConfiguration(ForwardedHeadersSettings.SectionName)
            .Validate(
                settings => settings.KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _))
                    && settings.KnownNetworks.All(network => IPNetwork.TryParse(network, out _)),
                "ForwardedHeaders entries must be valid IP addresses / CIDR networks.")
            .ValidateOnStart();

        static bool IsValid(FixedWindowSettings window) => window is { PermitLimit: > 0, WindowSeconds: > 0 };
    }

    private static void AddForwardedHeaders(IServiceCollection services) =>
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ForwardedHeadersSettings>>((options, settings) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in settings.Value.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in settings.Value.KnownNetworks)
            {
                options.KnownIPNetworks.Add(IPNetwork.Parse(network));
            }
        });

    private static void AddRateLimiting(IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var window = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Global;
                return RateLimitPartition.GetFixedWindowLimiter(ClientPartitionKey(context), _ => ToLimiterOptions(window));
            });

            options.AddPolicy(RateLimitPolicies.Strict, context =>
            {
                var window = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Strict;
                return RateLimitPartition.GetFixedWindowLimiter(ClientPartitionKey(context), _ => ToLimiterOptions(window));
            });

            options.AddPolicy(RateLimitPolicies.Search, context =>
            {
                var window = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Search;
                return RateLimitPartition.GetFixedWindowLimiter(ClientPartitionKey(context), _ => ToLimiterOptions(window));
            });

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = rejected.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = rejected.HttpContext,
                    ProblemDetails = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests." },
                }).ConfigureAwait(false);
            };
        });

    // Remote IP after forwarded-header processing, i.e. only trusted proxies can influence it.
    private static string ClientPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions ToLimiterOptions(FixedWindowSettings window) => new()
    {
        PermitLimit = window.PermitLimit,
        Window = TimeSpan.FromSeconds(window.WindowSeconds),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
