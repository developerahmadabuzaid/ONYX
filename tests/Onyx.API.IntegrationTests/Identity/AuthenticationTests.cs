using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.Identity;

public sealed class AuthenticationTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Non_privileged_users_sign_in_directly_and_me_returns_server_resolved_permissions()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);
        using var client = AuthorizedClient(tokens.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", Token);

        Assert.Equal("sales@onyx.test", me!.Email);
        Assert.Contains(Permissions.Leads.View, me.Permissions);
        Assert.DoesNotContain(Permissions.Users.Manage, me.Permissions);
        Assert.True(tokens.AccessTokenExpiresAtUtc <= Time.GetUtcNow().AddMinutes(10));
        Assert.Contains(await QueryAsync(db => db.AuditLogs), log => log.Action == AuditAction.LoginSucceeded);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_account_return_the_same_generic_401()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        using var wrongPassword = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sales@onyx.test", "wrong password here"), Token);
        using var unknown = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@onyx.test", "wrong password here"), Token);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var first = await wrongPassword.Content.ReadFromJsonAsync<ApiProblem>(Token);
        var second = await unknown.Content.ReadFromJsonAsync<ApiProblem>(Token);
        Assert.Equal(first!.Code, second!.Code);
        Assert.Equal(first.Title, second.Title);
        Assert.Equal("Auth.InvalidCredentials", first.Code);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_correct_password()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("sales@onyx.test", "not the password")).Status);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("sales@onyx.test", StrongPassword)).Status);
        Assert.Contains(await QueryAsync(db => db.AuditLogs), log => log.Action == AuditAction.LockedOut);
    }

    [Fact]
    public async Task Invited_and_disabled_accounts_cannot_sign_in()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("invited@onyx.test", password: null, SystemRoles.Sales);
        var disabledId = await CreateUserAsync("disabled@onyx.test", StrongPassword, SystemRoles.Sales);
        await using (var db = await DbAsync())
        {
            var user = await db.Users.FindAsync([disabledId], Token);
            user!.IsActive = false;
            await db.SaveChangesAsync(Token);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("invited@onyx.test", StrongPassword)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("disabled@onyx.test", StrongPassword)).Status);
    }

    [Fact]
    public async Task SuperAdmin_must_enrol_an_authenticator_before_any_token_is_issued()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);

        var (_, login) = await LoginAsync("owner@onyx.test", StrongPassword);
        Assert.Equal(LoginStatus.TwoFactorEnrollmentRequired, login!.Status);
        Assert.Null(login.Tokens);

        var enrolled = await EnrolAsync(login.ChallengeToken!);
        Assert.Equal(10, enrolled.RecoveryCodes.Count);
        Assert.NotEmpty(enrolled.Tokens.AccessToken);

        // The enrolment token is single-use.
        using var reuse = await Client.PostAsJsonAsync("/api/v1/auth/two-factor/enrollment/start", new TwoFactorEnrollmentStartRequest(login.ChallengeToken!), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var (_, second) = await LoginAsync("owner@onyx.test", StrongPassword);
        Assert.Equal(LoginStatus.TwoFactorRequired, second!.Status);
    }

    [Fact]
    public async Task Users_holding_privileged_permissions_must_also_enrol()
    {
        Fixture.RequireDatabase();
        await CreateRoleAsync("Auditors", Permissions.AuditLogs.View, Permissions.Users.Manage);
        await CreateUserAsync("auditor@onyx.test", StrongPassword, "Auditors");

        var (_, login) = await LoginAsync("auditor@onyx.test", StrongPassword);

        Assert.Equal(LoginStatus.TwoFactorEnrollmentRequired, login!.Status);
    }

    [Fact]
    public async Task Authenticator_codes_cannot_be_replayed()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        var key = await EnableTwoFactorAsync(userId);
        var code = TestTotp.Code(key, Time.GetUtcNow());

        var (_, first) = await LoginAsync("owner@onyx.test", StrongPassword);
        using var accepted = await Client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(first!.ChallengeToken!, code, null), Token);
        var (_, second) = await LoginAsync("owner@onyx.test", StrongPassword);
        using var replayed = await Client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(second!.ChallengeToken!, code, null), Token);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    [Fact]
    public async Task Recovery_codes_work_exactly_once()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        var (_, login) = await LoginAsync("owner@onyx.test", StrongPassword);
        var recoveryCode = (await EnrolAsync(login!.ChallengeToken!)).RecoveryCodes[0];

        var (_, first) = await LoginAsync("owner@onyx.test", StrongPassword);
        using var used = await Client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(first!.ChallengeToken!, null, recoveryCode), Token);
        var (_, second) = await LoginAsync("owner@onyx.test", StrongPassword);
        using var reused = await Client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(second!.ChallengeToken!, null, recoveryCode), Token);

        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Contains(await QueryAsync(db => db.AuditLogs), log => log.Action == AuditAction.TwoFactorRecoveryCodeUsed);
    }

    [Fact]
    public async Task The_second_factor_challenge_dies_after_five_wrong_codes()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        var key = await EnableTwoFactorAsync(userId);
        var (_, login) = await LoginAsync("owner@onyx.test", StrongPassword);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await Client.PostAsJsonAsync("/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(login!.ChallengeToken!, "000000", null), Token);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var correctButDead = await Client.PostAsJsonAsync(
            "/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(login!.ChallengeToken!, TestTotp.Code(key, Time.GetUtcNow()), null), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, correctButDead.StatusCode);
    }

    [Fact]
    public async Task Authenticator_keys_and_recovery_codes_are_encrypted_at_rest()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("owner@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        var (_, login) = await LoginAsync("owner@onyx.test", StrongPassword);
        using var start = await Client.PostAsJsonAsync("/api/v1/auth/two-factor/enrollment/start", new TwoFactorEnrollmentStartRequest(login!.ChallengeToken!), Token);
        var key = (await start.Content.ReadFromJsonAsync<TwoFactorEnrollmentStartResponse>(Token))!.SharedKey;
        using var complete = await Client.PostAsJsonAsync(
            "/api/v1/auth/two-factor/enrollment/complete", new TwoFactorEnrollmentCompleteRequest(login.ChallengeToken!, TestTotp.Code(key, Time.GetUtcNow())), Token);
        var codes = (await complete.Content.ReadFromJsonAsync<TwoFactorEnrollmentCompleteResponse>(Token))!.RecoveryCodes;

        var stored = await QueryAsync(db => db.UserTokens.Select(token => token.Value));
        var compactKey = key.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        Assert.NotEmpty(stored);
        Assert.All(stored, value =>
        {
            Assert.DoesNotContain(compactKey, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.All(codes, code => Assert.DoesNotContain(code, value ?? string.Empty, StringComparison.Ordinal));
        });
    }
}
