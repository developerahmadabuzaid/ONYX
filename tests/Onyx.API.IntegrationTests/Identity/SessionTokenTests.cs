using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.Identity;

namespace Onyx.API.IntegrationTests.Identity;

public sealed class SessionTokenTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string Me = "/api/v1/auth/me";

    [Fact]
    public async Task Refresh_rotates_and_a_concurrent_retry_within_grace_receives_the_same_successor()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var initial = await SignInAsync("sales@onyx.test", StrongPassword);

        var rotated = await RefreshAsync(initial.RefreshToken);
        Time.Advance(TimeSpan.FromSeconds(5));
        var retried = await RefreshAsync(initial.RefreshToken);

        Assert.NotEqual(initial.RefreshToken, rotated!.RefreshToken);
        Assert.Equal(rotated.RefreshToken, retried!.RefreshToken);
        using var client = AuthorizedClient(retried.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Me, Token)).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_consumed_refresh_token_outside_grace_revokes_the_whole_session_and_alerts_admins()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var initial = await SignInAsync("sales@onyx.test", StrongPassword);
        var rotated = await RefreshAsync(initial.RefreshToken);

        Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await RefreshAsync(initial.RefreshToken));

        // Both the legitimate successor and its access token are now dead.
        Assert.Null(await RefreshAsync(rotated!.RefreshToken));
        using var client = AuthorizedClient(rotated.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Me, Token)).StatusCode);

        Assert.Contains(await QueryAsync(db => db.AuditLogs), log => log.Action == AuditAction.RefreshTokenReuseDetected);
        var alert = Assert.Single(await QueryAsync(db => db.AdminNotifications));
        Assert.Equal(NotificationType.SecurityAlert, alert.Type);
        Assert.Equal(Permissions.Users.Manage, alert.RequiredPermission);
    }

    [Fact]
    public async Task Sessions_end_after_the_idle_timeout()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);

        Time.Advance(TimeSpan.FromMinutes(61));

        Assert.Null(await RefreshAsync(tokens.RefreshToken));
    }

    [Fact]
    public async Task Sessions_end_at_the_absolute_lifetime_even_when_kept_active()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);

        // Refresh every 50 minutes (inside the 60-minute idle limit). 14 refreshes = 700 min (< 12 h) must succeed;
        // the 15th, at 750 min, is past the 720-minute absolute limit and must fail.
        for (var i = 1; i <= 16; i++)
        {
            Time.Advance(TimeSpan.FromMinutes(50));
            var next = await RefreshAsync(tokens.RefreshToken);
            if (next is null)
            {
                Assert.Equal(15, i);
                return;
            }

            tokens = next;
        }

        Assert.Fail("Session outlived its 12-hour absolute lifetime.");
    }

    [Fact]
    public async Task Access_tokens_expire_after_ten_minutes()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);
        using var client = AuthorizedClient(tokens.AccessToken);

        Time.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Me, Token)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_session_immediately()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);
        using var client = AuthorizedClient(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Me, Token)).StatusCode);

        using var logout = await Client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(tokens.RefreshToken), Token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Me, Token)).StatusCode);
        Assert.Null(await RefreshAsync(tokens.RefreshToken));
    }

    [Fact]
    public async Task Disabling_a_user_invalidates_their_outstanding_access_token()
    {
        Fixture.RequireDatabase();
        var (_, admin) = await SignInSuperAdminAsync();
        var salesId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);
        using var sales = AuthorizedClient(tokens.AccessToken);

        using var disabled = await admin.PostAsync($"/api/v1/admin/users/{salesId}/disable", null, Token);

        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await sales.GetAsync(Me, Token)).StatusCode);
        Assert.Null(await RefreshAsync(tokens.RefreshToken));
        admin.Dispose();
    }

    [Fact]
    public async Task Changing_the_password_ends_every_session()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var first = await SignInAsync("sales@onyx.test", StrongPassword);
        var second = await SignInAsync("sales@onyx.test", StrongPassword);
        using var client = AuthorizedClient(first.AccessToken);

        using var change = await client.PostAsJsonAsync("/api/v1/auth/password/change", new ChangePasswordRequest(StrongPassword, "a brand new long passphrase"), Token);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Null(await RefreshAsync(first.RefreshToken));
        Assert.Null(await RefreshAsync(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("sales@onyx.test", StrongPassword)).Status);
    }

    [Fact]
    public async Task Refresh_tokens_and_challenges_are_stored_only_as_hashes()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var tokens = await SignInAsync("sales@onyx.test", StrongPassword);

        var stored = Assert.Single(await QueryAsync(db => db.RefreshTokens));

        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
        Assert.Equal(TokenHashing.Hash(tokens.RefreshToken), stored.TokenHash);
    }

    [Fact]
    public async Task Forged_tampered_and_misdirected_tokens_are_rejected()
    {
        Fixture.RequireDatabase();
        var userId = await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var real = await SignInAsync("sales@onyx.test", StrongPassword);
        var parsed = new JsonWebToken(real.AccessToken);
        var claims = parsed.Claims.Where(claim => claim.Type is "sub" or "sid" or "sst").ToList();

        string Sign(byte[] key, string audience) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "onyx-api",
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = Time.GetUtcNow().AddMinutes(5).UtcDateTime,
            NotBefore = Time.GetUtcNow().AddMinutes(-1).UtcDateTime,
            IssuedAt = Time.GetUtcNow().UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });

        var realKey = Convert.FromBase64String(Factory.SigningKey);
        var candidates = new Dictionary<string, string>
        {
            ["wrong key"] = Sign(new byte[48], "onyx-web"),
            ["wrong audience"] = Sign(realKey, "someone-else"),
            ["tampered payload"] = Tamper(real.AccessToken),
            ["unsigned (alg none)"] = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: "onyx-api", audience: "onyx-web", claims: claims, expires: Time.GetUtcNow().AddMinutes(5).UtcDateTime)),
        };

        foreach (var (name, token) in candidates)
        {
            using var client = AuthorizedClient(token);
            Assert.True((await client.GetAsync(Me, Token)).StatusCode == HttpStatusCode.Unauthorized, $"Accepted a token with {name}.");
        }

        // Control: the correctly signed token for the real session is accepted.
        using var control = AuthorizedClient(Sign(realKey, "onyx-web"));
        Assert.Equal(HttpStatusCode.OK, (await control.GetAsync(Me, Token)).StatusCode);
        Assert.NotEqual(Guid.Empty, userId);
    }

    private static string Tamper(string token)
    {
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        parts[1] = Base64UrlEncoder.Encode(payload.Replace("\"exp\":", "\"exp\":9", StringComparison.Ordinal));
        return string.Join('.', parts);
    }

    private async Task<TokenPairResponse?> RefreshAsync(string refreshToken)
    {
        using var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(refreshToken), Token);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TokenPairResponse>(Token) : null;
    }
}
