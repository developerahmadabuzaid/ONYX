using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Onyx.Application.Identity;
using Onyx.Infrastructure.Email.Templates;
using Onyx.Infrastructure.Identity;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Admin identity for Onyx.API only: ASP.NET Core Identity (no cookies), JWT bearer validation with per-request session
    /// checks, rotating refresh tokens, TOTP, and the Data Protection key ring stored in PostgreSQL.
    /// </summary>
    public static IServiceCollection AddOnyxIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<SessionOptions>().Bind(configuration.GetSection(SessionOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<IdentitySecurityOptions>().Bind(configuration.GetSection(IdentitySecurityOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<IdentitySecurityOptions>, IdentitySecurityOptionsValidator>();

        services.AddDataProtection().SetApplicationName("Onyx.Backend");
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider =>
            new ConfigureOptions<KeyManagementOptions>(options => options.XmlRepository = new DbXmlRepository(provider.GetRequiredService<IServiceScopeFactory>())));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderOptions.ProviderName;
                options.Tokens.AuthenticatorIssuer = "Onyx Vanguard Solutions";
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProviderOptions.ProviderName)
            .AddTokenProvider<InvitationTokenProvider>(InvitationTokenProviderOptions.ProviderName)
            .AddTokenProvider<AuthenticatorTokenProvider<ApplicationUser>>(TokenOptions.DefaultAuthenticatorProvider);

        // Replace the default store so authenticator keys and recovery codes are encrypted at rest.
        services.AddScoped<IUserStore<ApplicationUser>, ProtectedTokenUserStore>();

        services.AddOptions<PasswordResetTokenProviderOptions>().Configure<IOptions<IdentitySecurityOptions>>((options, security) =>
            options.TokenLifespan = TimeSpan.FromMinutes(security.Value.PasswordResetLifetimeMinutes));
        services.AddOptions<InvitationTokenProviderOptions>().Configure<IOptions<IdentitySecurityOptions>>((options, security) =>
            options.TokenLifespan = TimeSpan.FromHours(security.Value.InvitationLifetimeHours));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>(ConfigureJwtBearer);

        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<SessionValidationCache>();
        services.AddSingleton<PasswordTimingEqualizer>();
        services.AddSingleton<AdminLinkBuilder>();
        services.AddScoped<SessionManager>();
        services.AddScoped<RoleCatalog>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();

        // Data-subject requests also export and erase admin accounts, so they live with the identity services (API host only).
        services.AddScoped<Application.Privacy.IAccountPrivacyStore, AccountPrivacyStore>();
        services.AddScoped<Application.Privacy.IDataSubjectRequestService, Application.Privacy.DataSubjectRequestService>();
        services.AddScoped<IRoleAdministrationService, RoleAdministrationService>();
        services.AddScoped<IIdentityBootstrapService, IdentityBootstrapService>();

        services.AddSingleton<IEmailTemplate, InvitationEmailTemplate>();
        services.AddSingleton<IEmailTemplate, PasswordResetEmailTemplate>();

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions options, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)
    {
        var jwt = jwtOptions.Value;
        var skew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds);

        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.GetSigningKeyBytes()),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = skew,
            NameClaimType = OnyxClaimTypes.Subject,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                return expires is { } expiry && now <= expiry.Add(skew) && (notBefore is null || now >= notBefore.Value.Subtract(skew));
            },
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (!Guid.TryParse(principal?.FindFirst(OnyxClaimTypes.Subject)?.Value, out var userId)
                    || !Guid.TryParse(principal?.FindFirst(OnyxClaimTypes.Session)?.Value, out var sessionId)
                    || principal?.FindFirst(OnyxClaimTypes.SecurityStampHash)?.Value is not { Length: > 0 } stampHash)
                {
                    context.Fail("Token is missing required claims.");
                    return;
                }

                var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                if (!await validator.IsValidAsync(userId, sessionId, stampHash, context.HttpContext.RequestAborted).ConfigureAwait(false))
                {
                    context.Fail("Session is no longer valid.");
                }
            },
        };
    }
}
