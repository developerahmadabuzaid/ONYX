using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.Configuration;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Identity;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>The <c>reset-two-factor</c> operator command: break-glass 2FA recovery for the last enrolled SuperAdmin.</summary>
public sealed class OperatorTwoFactorRecoveryTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string Owner = "owner@onyx.test";
    private const string Reason = "Owner lost their phone; identity verified in person.";
    private const string Operator = "ops-user@ops-host";

    [Fact]
    public async Task Recovery_removes_every_second_factor_artefact_and_ends_all_sessions_and_sign_in_steps()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        var key = await EnableTwoFactorAsync(userId);
        var tokens = await SignInAsync(Owner, StrongPassword, key);
        var openChallenge = (await LoginAsync(Owner, StrongPassword)).Body!.ChallengeToken!;
        var recoveryCodes = await InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.ToList();
            await users.SetAuthenticationTokenAsync(user, "[Onyx]", "AuthenticatorKeyEnrollment", "stale");
            await users.SetLockoutEndDateAsync(user, Time.GetUtcNow().AddHours(1));
            return codes;
        });
        var stampBefore = (await FindAsync(userId)).SecurityStamp;

        var result = await RecoverAsync(Owner);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value!.UserId);
        Assert.Equal(1, result.Value.SessionsRevoked);
        Assert.True(result.Value.ChallengesClosed >= 1);

        var user = await FindAsync(userId);
        Assert.False(user.TwoFactorEnabled);
        Assert.Null(user.LastTotpTimeStep);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
        Assert.Empty(await QueryAsync(db => db.UserTokens.Where(token => token.UserId == userId)));
        Assert.All(await QueryAsync(db => db.UserSessions.Where(session => session.UserId == userId)), session => Assert.NotNull(session.RevokedAtUtc));

        // The old session, the open sign-in step, the old key and the old recovery codes are all useless now.
        using (var me = await AuthorizedClient(tokens.AccessToken).GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative), Token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }

        using (var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken), Token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        Time.Advance(TimeSpan.FromSeconds(30));
        using (var oldStep = await Client.PostAsJsonAsync(
            "/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(openChallenge, TestTotp.Code(key, Time.GetUtcNow()), null), Token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldStep.StatusCode);
        }

        using (var oldRecoveryCode = await Client.PostAsJsonAsync(
            "/api/v1/auth/login/two-factor", new TwoFactorLoginRequest(openChallenge, null, recoveryCodes[0]), Token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldRecoveryCode.StatusCode);
        }

        // The password still works, and leads straight to mandatory enrolment of a new authenticator.
        var login = await LoginAsync(Owner, StrongPassword);
        Assert.Equal(LoginStatus.TwoFactorEnrollmentRequired, login.Body!.Status);
        var enrolled = await EnrolAsync(login.Body.ChallengeToken!);
        Assert.NotEmpty(enrolled.Tokens.AccessToken);
    }

    [Fact]
    public async Task Recovery_writes_a_security_audit_record_without_authentication_data()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        var key = await EnableTwoFactorAsync(userId);

        Assert.True((await RecoverAsync(Owner)).IsSuccess);

        var record = Assert.Single(await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.TwoFactorRecoveredByOperator)));
        Assert.Equal(AuditCategory.Security, record.Category);
        Assert.Equal("User", record.EntityType);
        Assert.Equal(userId.ToString(), record.EntityId);
        Assert.Contains(Reason, record.Changes, StringComparison.Ordinal);
        Assert.Contains(Operator, record.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain(key, record.Changes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(StrongPassword, record.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain((await FindAsync(userId)).SecurityStamp!, record.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recovery_is_refused_while_another_enrolled_SuperAdmin_can_reset_the_account()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(userId);
        var colleague = await CreateUserAsync("second@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(colleague);

        var result = await RecoverAsync(Owner);

        Assert.Equal("Recovery.NotLastSuperAdmin", result.Error!.Code);
        await AssertUntouchedAsync(userId);
    }

    [Fact]
    public async Task Recovery_is_allowed_when_other_SuperAdmins_are_disabled_or_not_enrolled()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(userId);
        await CreateUserAsync("not-enrolled@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        var disabled = await CreateUserAsync("disabled@onyx.test", StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(disabled);
        await InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(disabled.ToString()))!;
            user.IsActive = false;
            return await users.UpdateAsync(user);
        });

        Assert.True((await RecoverAsync(Owner)).IsSuccess);
    }

    [Fact]
    public async Task Recovery_targets_only_existing_active_SuperAdmins()
    {
        Fixture.RequireDatabase();
        var sales = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        await EnableTwoFactorAsync(sales);
        var inactive = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(inactive);
        await InScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(inactive.ToString()))!;
            user.IsActive = false;
            return await users.UpdateAsync(user);
        });

        Assert.Equal("Recovery.NotASuperAdmin", (await RecoverAsync("nobody@onyx.test")).Error!.Code);
        Assert.Equal("Recovery.NotASuperAdmin", (await RecoverAsync("sales@onyx.test")).Error!.Code);
        Assert.Equal("Recovery.Inactive", (await RecoverAsync(Owner)).Error!.Code);
        await AssertUntouchedAsync(sales);
        await AssertUntouchedAsync(inactive);
        Assert.Empty(await QueryAsync(db => db.AuditLogs.Where(log => log.Action == AuditAction.TwoFactorRecoveredByOperator)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    [InlineData("line one\nline two of the reason")]
    public async Task Recovery_requires_a_meaningful_single_line_reason(string reason)
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync(Owner, StrongPassword, SystemRoles.SuperAdmin);
        await EnableTwoFactorAsync(userId);

        var result = await InScopeAsync(services =>
            services.GetRequiredService<IIdentityBootstrapService>().RecoverSuperAdminTwoFactorAsync(Owner, reason, Operator, Token));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        await AssertUntouchedAsync(userId);
    }

    [Theory]
    [InlineData("reset-two-factor")]
    [InlineData("reset-two-factor", "--email", Owner)]
    [InlineData("reset-two-factor", "--email", Owner, "--confirm", "other@onyx.test", "--reason", Reason)]
    [InlineData("reset-two-factor", "--email", Owner, "--confirm", Owner, "--reason", "short")]
    [InlineData("reset-two-factor", "--email", Owner, "--confirm", Owner, "--reason", Reason, "--force", "yes")]
    [InlineData("reset-two-factor", "--email", Owner, "--email", Owner, "--reason", Reason)]
    [InlineData("reset-two-factor", "--email", "--confirm", "--confirm", Owner, "--reason", Reason)]
    [InlineData("reset-two-factor", "--emial", Owner, "--confirm", Owner, "--reason", Reason)]
    public void Command_arguments_are_rejected_before_any_service_is_touched(params string[] args) =>
        Assert.NotNull(AdminCommandRunner.ResetTwoFactorArgumentError(args));

    [Fact]
    public void Well_formed_command_arguments_are_accepted_in_any_order()
    {
        Assert.Null(AdminCommandRunner.ResetTwoFactorArgumentError(["reset-two-factor", "--email", Owner, "--confirm", Owner, "--reason", Reason]));
        Assert.Null(AdminCommandRunner.ResetTwoFactorArgumentError(["reset-two-factor", "--reason", Reason, "--confirm", "OWNER@onyx.test", "--email", Owner]));
    }

    [Theory]
    [InlineData("POST", "/api/v1/admin/users/reset-two-factor")]
    [InlineData("POST", "/api/v1/auth/two-factor/recover")]
    [InlineData("GET", "/reset-two-factor")]
    public async Task The_recovery_command_has_no_HTTP_surface(string method, string path)
    {
        Fixture.RequireDatabase();

        using var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)), Token);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.Unauthorized, $"{method} {path} returned {response.StatusCode}");
    }

    private Task<Result<TwoFactorRecoveryResult>> RecoverAsync(string email) =>
        InScopeAsync(services => services.GetRequiredService<IIdentityBootstrapService>().RecoverSuperAdminTwoFactorAsync(email, Reason, Operator, Token));

    private Task<ApplicationUser> FindAsync(Guid userId) =>
        InScopeAsync(async services => (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString()))!);

    private async Task AssertUntouchedAsync(Guid userId)
    {
        var user = await FindAsync(userId);
        Assert.True(user.TwoFactorEnabled);
        Assert.NotEmpty(await QueryAsync(db => db.UserTokens.Where(token => token.UserId == userId && token.Name == "AuthenticatorKey")));
    }
}
