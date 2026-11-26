using System.Net;
using System.Net.Http.Json;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Customers;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Customers;
using Onyx.Domain.Customers;

namespace Onyx.API.IntegrationTests.Commerce;

/// <summary>
/// Customer identity through the real API and PostgreSQL: isolation from staff (in both directions), sign-up without account
/// enumeration, single-use verification, lockout, password reset and sign-out. All data is fictional sample data.
/// </summary>
public sealed class CustomerIdentityTests(PostgresFixture fixture) : CommerceApiTestBase(fixture)
{
    [Fact]
    public async Task Customer_sessions_never_open_staff_endpoints_and_staff_tokens_never_open_customer_endpoints()
    {
        Fixture.RequireDatabase();
        using var customer = await NewCustomerAsync("isolated@example.test");
        using var staff = await SignInWithAsync("staff", Permissions.Products.View, Permissions.Customers.View);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(customer.GetAsync($"{Customer}/profile", Token)));
        foreach (var staffOnly in new[] { "/api/v1/auth/me", "/api/v1/admin/notifications", "/api/v1/admin/products", "/api/v1/admin/customers", "/api/v1/admin/delivery/projects" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(customer.GetAsync(staffOnly, Token)));
        }

        // The same opaque token presented as a staff bearer token is not a valid JWT either.
        var session = customer.DefaultRequestHeaders.Authorization!.Parameter!;
        using var asBearer = AuthorizedClient(session);
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(asBearer.GetAsync("/api/v1/auth/me", Token)));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(staff.GetAsync($"{Customer}/profile", Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.GetAsync($"{Customer}/profile", Token)));

        // Customers live in their own table and can never hold a staff role.
        Assert.Empty(await QueryAsync(db => db.Users.Where(user => user.Email == "isolated@example.test")));
    }

    [Fact]
    public async Task Sign_up_answers_the_same_for_new_and_existing_addresses_and_emails_the_owner_instead()
    {
        Fixture.RequireDatabase();
        using var first = await Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("owner@example.test"), Token);
        await VerifyAsync("owner@example.test");
        using var second = await Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("OWNER@example.test") with { Password = "an attacker passphrase" }, Token);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(await QueryAsync(db => db.CustomerAccounts.Where(account => account.NormalizedEmail == "OWNER@EXAMPLE.TEST")));
        Assert.Single(await QueryAsync(db => db.EmailMessages.Where(message => message.TemplateKey == CustomerEmailTemplates.AlreadyRegistered)));

        // The original password still works; the second attempt changed nothing.
        Assert.Equal("Sample Customer", (await CustomerSignInAsync("owner@example.test", CustomerPassword)).DisplayName);
    }

    [Fact]
    public async Task Sign_up_requires_terms_a_strong_password_and_a_verified_email_before_sign_in()
    {
        Fixture.RequireDatabase();
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("a@example.test") with { CaptchaToken = null }, Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync(
            $"{CustomerAuth}/register", Registration("a@example.test") with { CaptchaToken = Solved("inquiry") }, Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("a@example.test") with { AcceptTerms = false }, Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("a@example.test") with { Password = "short" }, Token)));
        Assert.Empty(await QueryAsync(db => db.CustomerAccounts));

        using var registered = await Client.PostAsJsonAsync($"{CustomerAuth}/register", Registration("a@example.test", "/en/subscribe?product=x&intent=trial"), Token);
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);

        using (var early = await Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn("a@example.test", CustomerPassword), Token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
            Assert.Contains("Customer.EmailNotVerified", await early.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }

        var body = await LatestEmailBodyAsync("a@example.test", CustomerEmailTemplates.VerifyEmail);
        Assert.Contains("https://onyx.test/en/verify-email?", body, StringComparison.Ordinal);
        Assert.Contains("returnUrl=%2Fen%2Fsubscribe%3Fproduct%3Dx%26intent%3Dtrial", body, StringComparison.Ordinal);

        var (id, token) = LinkParts(body);
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/verify-email", new VerifyCustomerEmailRequest(id, token), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/verify-email", new VerifyCustomerEmailRequest(id, token), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/verify-email", new VerifyCustomerEmailRequest(Guid.NewGuid(), token), Token)));

        var session = await CustomerSignInAsync("a@example.test", CustomerPassword);
        Assert.Equal(64, Assert.Single(await QueryAsync(db => db.CustomerSessions)).TokenHash.Length);
        Assert.DoesNotContain(await QueryAsync(db => db.CustomerSessions), stored => stored.TokenHash == session.SessionToken);
    }

    [Fact]
    public async Task Wrong_passwords_lock_the_account_with_the_same_answer_as_unknown_addresses()
    {
        Fixture.RequireDatabase();
        using var customer = await NewCustomerAsync("lock@example.test");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.PostAsJsonAsync(
                $"{CustomerAuth}/sign-in", SignIn("lock@example.test", "wrong passphrase " + attempt), Token)));
        }

        using var locked = await Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn("lock@example.test", CustomerPassword), Token);
        using var unknown = await Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn("nobody@example.test", CustomerPassword), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Contains("Customer.InvalidCredentials", await locked.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Contains("Customer.InvalidCredentials", await unknown.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);

        Time.Advance(TimeSpan.FromMinutes(16));
        Assert.NotNull(await CustomerSignInAsync("lock@example.test", CustomerPassword));
    }

    [Fact]
    public async Task Password_reset_works_once_and_ends_every_session_and_sign_out_revokes_the_session()
    {
        Fixture.RequireDatabase();
        using var customer = await NewCustomerAsync("reset@example.test");
        using var other = CustomerClient((await CustomerSignInAsync("reset@example.test", CustomerPassword)).SessionToken);

        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/forgot-password", new CustomerForgotPasswordRequest("nobody@example.test", "en", Solved("customer-recovery")), Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/forgot-password", new CustomerForgotPasswordRequest("reset@example.test", "en", Solved("customer-recovery")), Token)));
        Assert.Single(await QueryAsync(db => db.EmailMessages.Where(message => message.TemplateKey == CustomerEmailTemplates.PasswordReset)));

        var (id, token) = LinkParts(await LatestEmailBodyAsync("reset@example.test", CustomerEmailTemplates.PasswordReset));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/reset-password", new CustomerResetPasswordRequest(id, token, "a brand new sample passphrase"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/reset-password", new CustomerResetPasswordRequest(id, token, "yet another sample passphrase"), Token)));

        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(customer.GetAsync($"{Customer}/profile", Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(other.GetAsync($"{Customer}/profile", Token)));

        using var fresh = CustomerClient((await CustomerSignInAsync("reset@example.test", "a brand new sample passphrase")).SessionToken);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(fresh.GetAsync($"{Customer}/profile", Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(fresh.PostAsync($"{Customer}/sign-out", null, Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(fresh.GetAsync($"{Customer}/profile", Token)));
    }

    [Fact]
    public async Task Disabled_and_deleted_customers_lose_access_at_once_and_deletion_erases_personal_data()
    {
        Fixture.RequireDatabase();
        using var customer = await NewCustomerAsync("gone@example.test");
        using var manager = await SignInWithAsync("customers", Permissions.Customers.View, Permissions.Customers.Manage);
        var account = Assert.Single(await QueryAsync(db => db.CustomerAccounts));

        var detail = await ReadAsync<Contracts.Commerce.AdminCustomerResponse>(await manager.GetAsync($"/api/v1/admin/customers/{account.Id}", Token));
        await ReadAsync<Contracts.Commerce.AdminCustomerResponse>(await manager.PostAsJsonAsync(
            $"/api/v1/admin/customers/{account.Id}/status", new Contracts.Commerce.CustomerStatusRequest(false, detail.Version), Token));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(customer.GetAsync($"{Customer}/profile", Token)));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn("gone@example.test", CustomerPassword), Token)));

        detail = await ReadAsync<Contracts.Commerce.AdminCustomerResponse>(await manager.GetAsync($"/api/v1/admin/customers/{account.Id}", Token));
        await ReadAsync<Contracts.Commerce.AdminCustomerResponse>(await manager.PostAsJsonAsync(
            $"/api/v1/admin/customers/{account.Id}/status", new Contracts.Commerce.CustomerStatusRequest(true, detail.Version), Token));
        using var again = CustomerClient((await CustomerSignInAsync("gone@example.test", CustomerPassword)).SessionToken);

        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(again.PostAsJsonAsync($"{Customer}/delete", new DeleteCustomerAccountRequest("wrong passphrase"), Token)));
        Assert.Equal(HttpStatusCode.NoContent, await StatusAsync(again.PostAsJsonAsync($"{Customer}/delete", new DeleteCustomerAccountRequest(CustomerPassword), Token)));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(again.GetAsync($"{Customer}/profile", Token)));

        var erased = Assert.Single(await QueryAsync(db => db.CustomerAccounts));
        Assert.Equal(CustomerAccountStatus.Erased, erased.Status);
        Assert.DoesNotContain("gone@", erased.Email, StringComparison.Ordinal);
        Assert.Equal("—", erased.DisplayName);
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(Client.PostAsJsonAsync($"{CustomerAuth}/sign-in", SignIn("gone@example.test", CustomerPassword), Token)));
    }
}
