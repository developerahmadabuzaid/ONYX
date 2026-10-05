using System.Net;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Leads;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>Without a Turnstile secret, protected submissions fail closed (503) and nothing is stored or sent anywhere.</summary>
public sealed class TurnstileNotConfiguredApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>(base.Settings)
    {
        ["Turnstile:SecretKey"] = string.Empty,
    };

    [Fact]
    public async Task Submissions_fail_closed_when_turnstile_is_not_configured()
    {
        Fixture.RequireDatabase();

        using var response = await SubmitAsync(InquiryTypes.Contact, Form());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Captcha.Unavailable", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(db => db.Inquiries));
        Assert.Empty(Turnstile.Requests);
    }
}
