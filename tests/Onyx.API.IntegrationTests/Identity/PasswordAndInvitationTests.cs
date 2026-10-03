using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.API.IntegrationTests.Identity;

public sealed partial class PasswordAndInvitationTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Forgot_password_never_reveals_whether_an_account_exists()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        using var known = await Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("sales@onyx.test"), Token);
        using var unknown = await Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("nobody@onyx.test"), Token);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        var email = Assert.Single(await QueryAsync(db => db.EmailMessages));
        Assert.Equal(PasswordResetEmailTemplate.TemplateKey, email.TemplateKey);
        Assert.Equal("sales@onyx.test", email.ToAddress);
    }

    [Fact]
    public async Task Password_reset_link_sets_a_new_password_ends_sessions_and_works_only_once()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var session = await SignInAsync("sales@onyx.test", StrongPassword);
        await Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("sales@onyx.test"), Token);
        var token = ExtractToken(Assert.Single(await QueryAsync(db => db.EmailMessages)).TextBody, AdminLinkPath.ResetPassword);

        using var reset = await Client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(userId, token, "a completely new passphrase"), Token);
        using var reused = await Client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(userId, token, "another new passphrase!!"), Token);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("sales@onyx.test", StrongPassword)).Status);
        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync("sales@onyx.test", "a completely new passphrase")).Body!.Status);
        using var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(session.RefreshToken), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Weak_passwords_are_rejected_with_validation_details()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        await Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("sales@onyx.test"), Token);
        var token = ExtractToken(Assert.Single(await QueryAsync(db => db.EmailMessages)).TextBody, AdminLinkPath.ResetPassword);

        using var weak = await Client.PostAsJsonAsync("/api/v1/auth/password/reset", new ResetPasswordRequest(userId, token, "short"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    [Fact]
    public async Task Invited_users_set_their_password_from_the_emailed_link_once_and_get_their_role()
    {
        Fixture.RequireDatabase();
        var (_, admin) = await SignInSuperAdminAsync();

        using var invite = await admin.PostAsJsonAsync(
            "/api/v1/admin/users", new InviteUserRequest("new.sales@onyx.test", "سارة", "ar", [SystemRoles.Sales]), Token);
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var invited = await invite.Content.ReadFromJsonAsync<UserSummaryResponse>(Token);
        Assert.False(invited!.EmailConfirmed);

        var email = Assert.Single(await QueryAsync(db => db.EmailMessages.Where(message => message.TemplateKey == InvitationEmailTemplate.TemplateKey)));
        Assert.Equal("ar", email.LanguageCode);
        Assert.Contains(OnyxApiFactory.AdminPortalBaseUrl + AdminLinkPath.AcceptInvitation, email.TextBody, StringComparison.Ordinal);
        var token = ExtractToken(email.TextBody, AdminLinkPath.AcceptInvitation);

        using var accept = await Client.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(invited.Id, token, StrongPassword), Token);
        using var replay = await Client.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(invited.Id, token, "attacker chosen passphrase"), Token);

        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync("new.sales@onyx.test", StrongPassword)).Body!.Status);
        Assert.Contains(await QueryAsync(db => db.AuditLogs), log => log.Action == AuditAction.InvitationAccepted);
        admin.Dispose();
    }

    private static string ExtractToken(string body, string path)
    {
        var match = LinkPattern().Matches(body).First(candidate => candidate.Value.Contains(path, StringComparison.Ordinal));
        return match.Groups["token"].Value;
    }

    [GeneratedRegex(@"https://\S+?\?userId=[0-9a-f-]+&token=(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex LinkPattern();

    private static class AdminLinkPath
    {
        public const string ResetPassword = "/admin/account/reset-password";
        public const string AcceptInvitation = "/admin/account/accept-invitation";
    }
}
