using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Identity;
using Onyx.Infrastructure.Security;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>
/// Cloudflare's always-pass test secret relaxes the action and hostname check only in Development. Outside Development (this
/// host runs as "Testing") a token for the wrong action or host is still refused, and Production refuses the test secret.
/// </summary>
public sealed class TurnstileTestKeyApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Turnstile:SecretKey"] = TurnstileTestKeys.AlwaysPassesSecret,
    };

    [Fact]
    public async Task Outside_development_the_test_secret_does_not_relax_the_action_or_hostname_check()
    {
        Fixture.RequireDatabase();
        foreach (var token in new[] { Solved("inquiry", AdminHost), Solved("admin-login", "evil.example") })
        {
            using var refused = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@onyx.test", StrongPassword, token), Token);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains("Captcha.Failed", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_relaxation_needs_both_development_and_the_published_test_secret_and_production_refuses_it()
    {
        Assert.True(TurnstileTestKeys.RelaxesActionAndHostname(isDevelopment: true, TurnstileTestKeys.AlwaysPassesSecret));
        Assert.False(TurnstileTestKeys.RelaxesActionAndHostname(isDevelopment: false, TurnstileTestKeys.AlwaysPassesSecret));
        Assert.False(TurnstileTestKeys.RelaxesActionAndHostname(isDevelopment: true, "a-real-secret"));
        Assert.False(TurnstileTestKeys.RelaxesActionAndHostname(isDevelopment: true, null));

        var options = new TurnstileOptions { SecretKey = TurnstileTestKeys.AlwaysPassesSecret, AllowedHostnames = ["onyx.test"] };
        Assert.False(options.IsValidFor(isProduction: true, "https://onyx.test", null));
        Assert.True(options.IsValidFor(isProduction: false, "https://onyx.test", null));
    }
}
