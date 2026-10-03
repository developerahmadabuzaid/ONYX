using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Identity;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Privacy;

namespace Onyx.API.IntegrationTests.Privacy;

public sealed class ConsentApiTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    private static readonly Uri Consents = new("/api/v1/consents", UriKind.Relative);

    [Fact]
    public async Task A_consent_receipt_is_stored_anonymously()
    {
        Fixture.RequireDatabase();
        var consentId = Guid.NewGuid();

        using var response = await Client.PostAsJsonAsync(Consents, new RecordCookieConsentRequest(consentId, "1", true, false, "ar"), Token);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var db = await DbAsync();
        var receipt = await db.CookieConsentReceipts.AsNoTracking().SingleAsync(Token);
        Assert.Equal(consentId, receipt.ConsentId);
        Assert.Equal("1", receipt.PolicyVersion);
        Assert.True(receipt.Analytics);
        Assert.False(receipt.Marketing);
        Assert.Equal("ar", receipt.LanguageCode);
        Assert.Equal(Time.GetUtcNow(), receipt.RecordedAtUtc);
        Assert.True(receipt.IpHash is null || receipt.IpHash.Length == 64);
    }

    [Fact]
    public async Task A_new_choice_for_the_same_browser_adds_a_receipt_instead_of_overwriting()
    {
        Fixture.RequireDatabase();
        var consentId = Guid.NewGuid();

        using var first = await Client.PostAsJsonAsync(Consents, new RecordCookieConsentRequest(consentId, "1", true, true, "en"), Token);
        using var second = await Client.PostAsJsonAsync(Consents, new RecordCookieConsentRequest(consentId, "2", false, false, "en"), Token);

        await using var db = await DbAsync();
        Assert.Equal(2, await db.CookieConsentReceipts.CountAsync(receipt => receipt.ConsentId == consentId, Token));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "1", "ar")]
    [InlineData("4b8f8a8e-3f7e-4c51-9a55-4c8f6a8b2e01", "", "ar")]
    [InlineData("4b8f8a8e-3f7e-4c51-9a55-4c8f6a8b2e01", "1 OR 1=1", "ar")]
    [InlineData("4b8f8a8e-3f7e-4c51-9a55-4c8f6a8b2e01", "1", "fr")]
    public async Task Invalid_receipts_are_rejected(string consentId, string policyVersion, string language)
    {
        Fixture.RequireDatabase();

        using var response = await Client.PostAsJsonAsync(Consents, new RecordCookieConsentRequest(Guid.Parse(consentId), policyVersion, false, false, language), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = await DbAsync();
        Assert.Equal(0, await db.CookieConsentReceipts.CountAsync(Token));
    }
}
