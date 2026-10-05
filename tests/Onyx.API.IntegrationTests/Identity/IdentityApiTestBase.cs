using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Persistence;
using Onyx.Application.Identity;
using Onyx.Contracts.Identity;
using Onyx.Infrastructure.Identity;
using Onyx.Infrastructure.Persistence;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>Real API + real PostgreSQL (disposable DB reset per test) with test-controlled time.</summary>
[Collection(DatabaseCollection.Name)]
public abstract class IdentityApiTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    protected const string StrongPassword = "correct horse battery staple";

    private OnyxApiFactory? _factory;

    protected PostgresFixture Fixture { get; } = fixture;

    protected ManualTimeProvider Time { get; } = new(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    protected OnyxApiFactory Factory => _factory ?? throw new InvalidOperationException("Database tests require ONYX_TEST_POSTGRES.");

    protected HttpClient Client { get; private set; } = null!;

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    protected virtual IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["RateLimiting:Strict:PermitLimit"] = "10000",
        ["RateLimiting:Global:PermitLimit"] = "10000",
    };

    public async ValueTask InitializeAsync()
    {
        if (!Fixture.IsAvailable)
        {
            return;
        }

        await Fixture.ApplicationDatabase!.ResetAsync(Token);
        _factory = new OnyxApiFactory("Testing", Fixture.ApplicationDatabase.ConnectionString, Settings, Time, ConfigureTestServices);
        Client = _factory.CreateClient();
        await InScopeAsync(services => services.GetRequiredService<IIdentityBootstrapService>().SeedRolesAsync(Token));
    }

    /// <summary>The Turnstile token sent with sign-in; null while the test host has no Turnstile configured.</summary>
    protected virtual string? LoginCaptchaToken() => null;

    /// <summary>Replaces services of the API under test (e.g. the HTTP handler behind an external provider adapter).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    protected async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected Task<ApplicationDbContext> DbAsync() => Task.FromResult(PostgresFixture.CreateApplicationContext(Fixture.ApplicationDatabase!.ConnectionString));

    protected Task<Guid> CreateUserAsync(string email, string? password, params string[] roles) =>
        InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Email = email,
                UserName = email,
                DisplayName = email.Split('@')[0],
                PreferredLanguage = "en",
                EmailConfirmed = password is not null,
                CreatedAtUtc = Time.GetUtcNow(),
            };
            var created = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
            if (roles.Length > 0)
            {
                Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
            }

            return user.Id;
        });

    /// <summary>Enrols an authenticator directly and returns its base32 key.</summary>
    protected Task<string> EnableTwoFactorAsync(Guid userId) =>
        InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            return (await users.GetAuthenticatorKeyAsync(user))!;
        });

    protected Task<Guid> CreateRoleAsync(string name, params string[] permissions) =>
        InScopeAsync(async services =>
        {
            var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var db = services.GetRequiredService<ApplicationDbContext>();
            var role = new ApplicationRole(name);
            Assert.True((await roles.CreateAsync(role)).Succeeded);
            db.RolePermissions.AddRange(permissions.Select(permission => new RolePermission { RoleId = role.Id, Permission = permission }));
            await db.SaveChangesAsync(Token);
            return role.Id;
        });

    protected async Task<(HttpStatusCode Status, LoginResponse? Body)> LoginAsync(string email, string password)
    {
        using var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password, LoginCaptchaToken()), Token);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LoginResponse>(Token) : null);
    }

    /// <summary>Full sign-in as the BFF would do it, including the second factor or mandatory enrolment.</summary>
    protected async Task<TokenPairResponse> SignInAsync(string email, string password, string? authenticatorKey = null)
    {
        var (status, login) = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, status);

        switch (login!.Status)
        {
            case LoginStatus.Succeeded:
                return login.Tokens!;

            case LoginStatus.TwoFactorRequired:
                Assert.NotNull(authenticatorKey);
                Time.Advance(TimeSpan.FromSeconds(30));
                using (var response = await Client.PostAsJsonAsync(
                    "/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(login.ChallengeToken!, TestTotp.Code(authenticatorKey, Time.GetUtcNow()), null), Token))
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    return (await response.Content.ReadFromJsonAsync<TokenPairResponse>(Token))!;
                }

            default:
                var enrollment = await EnrolAsync(login.ChallengeToken!);
                return enrollment.Tokens;
        }
    }

    protected async Task<TwoFactorEnrollmentCompleteResponse> EnrolAsync(string enrollmentToken)
    {
        using var start = await Client.PostAsJsonAsync("/api/v1/auth/two-factor/enrollment/start", new TwoFactorEnrollmentStartRequest(enrollmentToken), Token);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var key = (await start.Content.ReadFromJsonAsync<TwoFactorEnrollmentStartResponse>(Token))!.SharedKey;

        using var complete = await Client.PostAsJsonAsync(
            "/api/v1/auth/two-factor/enrollment/complete", new TwoFactorEnrollmentCompleteRequest(enrollmentToken, TestTotp.Code(key, Time.GetUtcNow())), Token);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return (await complete.Content.ReadFromJsonAsync<TwoFactorEnrollmentCompleteResponse>(Token))!;
    }

    /// <summary>A SuperAdmin with an enrolled authenticator, signed in.</summary>
    protected async Task<(Guid UserId, HttpClient Client)> SignInSuperAdminAsync(string email = "owner@onyx.test")
    {
        var userId = await CreateUserAsync(email, StrongPassword, SystemRoles.SuperAdmin);
        var key = await EnableTwoFactorAsync(userId);
        var tokens = await SignInAsync(email, StrongPassword, key);
        return (userId, AuthorizedClient(tokens.AccessToken));
    }

    protected HttpClient AuthorizedClient(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected async Task<List<T>> QueryAsync<T>(Func<ApplicationDbContext, IQueryable<T>> query)
    {
        await using var db = await DbAsync();
        return await query(db).ToListAsync(Token);
    }
}
