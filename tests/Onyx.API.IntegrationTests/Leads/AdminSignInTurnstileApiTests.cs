using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>
/// With Turnstile configured, admin sign-in and password-reset requests need a token solved on the admin host for their action;
/// a refused attempt never reaches the password check.
/// </summary>
public sealed class AdminSignInTurnstileApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    [Fact]
    public async Task Sign_in_needs_a_token_for_the_admin_login_action()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);

        foreach (var token in new string?[] { null, "fail", Solved("inquiry", AdminHost), Solved("admin-login", "evil.example") })
        {
            using var refused = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sales@onyx.test", StrongPassword, token), Token);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("Captcha.Failed", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        using var accepted = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sales@onyx.test", StrongPassword, Solved("admin-login", AdminHost)), Token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains(AuditAction.LoginFailed, await QueryAsync(db => db.AuditLogs.Select(log => log.Action)));
    }

    [Fact]
    public async Task Password_reset_requests_need_a_token_for_the_password_reset_action()
    {
        Fixture.RequireDatabase();

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest("someone@onyx.test"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot", new ForgotPasswordRequest("someone@onyx.test", Solved("admin-login", AdminHost)), Token)));
        Assert.Equal(HttpStatusCode.Accepted, await StatusAsync(Client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot", new ForgotPasswordRequest("someone@onyx.test", Solved("password-reset", AdminHost)), Token)));
    }
}
