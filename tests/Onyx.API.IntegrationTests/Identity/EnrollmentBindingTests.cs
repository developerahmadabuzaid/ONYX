using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Infrastructure.Identity;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>A pending authenticator key completes enrolment only with the challenge whose start step generated it.</summary>
public sealed class EnrollmentBindingTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string Email = "owner@onyx.test";

    [Fact]
    public async Task A_stale_pending_key_from_an_abandoned_setup_cannot_complete_enrolment()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Email, StrongPassword, SystemRoles.SuperAdmin);
        var staleKey = await SetPendingKeyWithoutStartAsync(userId);

        var challenge = await EnrollmentChallengeAsync();
        using var response = await CompleteAsync(challenge, staleKey);

        await AssertRejectedAsync(response, userId);
    }

    [Fact]
    public async Task A_key_issued_to_one_setup_attempt_cannot_complete_another()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Email, StrongPassword, SystemRoles.SuperAdmin);
        var first = await EnrollmentChallengeAsync();
        var second = await EnrollmentChallengeAsync();

        var firstKey = await StartAsync(first);
        var secondKey = await StartAsync(second);

        // The second start replaced the key and re-bound it: the first attempt can't use either key.
        using (var withCurrentKey = await CompleteAsync(first, secondKey))
        {
            await AssertRejectedAsync(withCurrentKey, userId);
        }

        using (var withOwnOldKey = await CompleteAsync(first, firstKey))
        {
            await AssertRejectedAsync(withOwnOldKey, userId);
        }

        using var legitimate = await CompleteAsync(second, secondKey);
        Assert.Equal(HttpStatusCode.OK, legitimate.StatusCode);
    }

    [Fact]
    public async Task A_challenge_that_never_ran_its_own_setup_cannot_reuse_another_attempts_key()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Email, StrongPassword, SystemRoles.SuperAdmin);
        var started = await EnrollmentChallengeAsync();
        var key = await StartAsync(started);
        var neverStarted = await EnrollmentChallengeAsync();

        using var response = await CompleteAsync(neverStarted, key);

        await AssertRejectedAsync(response, userId);
        using var legitimate = await CompleteAsync(started, key);
        Assert.Equal(HttpStatusCode.OK, legitimate.StatusCode);
    }

    [Fact]
    public async Task The_binding_is_removed_once_enrolment_completes()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Email, StrongPassword, SystemRoles.SuperAdmin);
        var challenge = await EnrollmentChallengeAsync();
        var key = await StartAsync(challenge);
        Assert.NotNull(await BindingAsync(userId));

        using var response = await CompleteAsync(challenge, key);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await BindingAsync(userId));
    }

    [Fact]
    public async Task After_an_admin_2FA_reset_the_leftover_key_cannot_complete_enrolment()
    {
        Fixture.RequireDatabase();
        var (_, admin) = await SignInSuperAdminAsync("admin@onyx.test");
        var userId = await CreateUserAsync(Email, StrongPassword, SystemRoles.SuperAdmin);
        await EnrolAsync(await EnrollmentChallengeAsync());

        using (var reset = await admin.PostAsync(new Uri($"/api/v1/admin/users/{userId}/two-factor/reset", UriKind.Relative), null, Token))
        {
            Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        }

        Assert.Null(await BindingAsync(userId));
        var leftoverKey = await PendingKeyAsync(userId);
        var challenge = await EnrollmentChallengeAsync();
        using var response = await CompleteAsync(challenge, leftoverKey);

        await AssertRejectedAsync(response, userId);
        admin.Dispose();
    }

    private async Task<string> EnrollmentChallengeAsync()
    {
        var (status, login) = await LoginAsync(Email, StrongPassword);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(LoginStatus.TwoFactorEnrollmentRequired, login!.Status);
        return login.ChallengeToken!;
    }

    private async Task<string> StartAsync(string challenge)
    {
        using var start = await Client.PostAsJsonAsync("/api/v1/auth/two-factor/enrollment/start", new TwoFactorEnrollmentStartRequest(challenge), Token);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        return (await start.Content.ReadFromJsonAsync<TwoFactorEnrollmentStartResponse>(Token))!.SharedKey;
    }

    private Task<HttpResponseMessage> CompleteAsync(string challenge, string key) =>
        Client.PostAsJsonAsync(
            "/api/v1/auth/two-factor/enrollment/complete",
            new TwoFactorEnrollmentCompleteRequest(challenge, TestTotp.Code(key, Time.GetUtcNow())),
            Token);

    private async Task AssertRejectedAsync(HttpResponseMessage response, Guid userId)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidChallenge", (await response.Content.ReadFromJsonAsync<ApiProblem>(Token))!.Code);
        var user = await InScopeAsync(services => services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString()));
        Assert.False(user!.TwoFactorEnabled);
    }

    /// <summary>Simulates a key left behind outside the current setup flow (e.g. created before the binding existed).</summary>
    private Task<string> SetPendingKeyWithoutStartAsync(Guid userId) =>
        InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            await users.ResetAuthenticatorKeyAsync(user);
            return (await users.GetAuthenticatorKeyAsync(user))!;
        });

    private Task<string> PendingKeyAsync(Guid userId) =>
        InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            return (await users.GetAuthenticatorKeyAsync((await users.FindByIdAsync(userId.ToString()))!))!;
        });

    private Task<string?> BindingAsync(Guid userId) =>
        InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            return await users.GetAuthenticationTokenAsync((await users.FindByIdAsync(userId.ToString()))!, "[Onyx]", "AuthenticatorKeyEnrollment");
        });
}
