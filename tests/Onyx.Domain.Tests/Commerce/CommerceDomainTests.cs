using Onyx.Domain.Billing;
using Onyx.Domain.Customers;
using Onyx.Domain.Products;
using Onyx.Domain.Subscriptions;

namespace Onyx.Domain.Tests.Commerce;

/// <summary>Rules of the SaaS platform: customer accounts, tokens and sessions, the proof and price guards, subscriptions and invoices.</summary>
public sealed class CommerceDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Customers_register_unverified_with_terms_and_change_the_stamp_with_credentials()
    {
        Assert.True(CustomerAccount.Register("not-an-email", "Name", null, "en", "hash", "1", false, Now).IsFailure);
        Assert.True(CustomerAccount.Register("a@example.test", " ", null, "en", "hash", "1", false, Now).IsFailure);
        Assert.True(CustomerAccount.Register("a@example.test", "Name", null, "fr", "hash", "1", false, Now).IsFailure);

        var account = CustomerAccount.Register(" A@Example.test ", "Sample", "Sample Co", "ar", "hash", "1", true, Now).Value;
        Assert.Equal(CustomerAccountStatus.PendingVerification, account.Status);
        Assert.False(account.CanSignIn);
        Assert.Equal("A@EXAMPLE.TEST", account.NormalizedEmail);
        Assert.True(account.MarketingConsent);

        Assert.True(account.VerifyEmail(Now).IsSuccess);
        Assert.True(account.CanSignIn);
        var stamp = account.SecurityStamp;
        account.ChangePassword("new-hash");
        Assert.NotEqual(stamp, account.SecurityStamp);

        account.Disable();
        Assert.False(account.CanSignIn);
        Assert.True(account.Enable().IsSuccess);
        Assert.True(account.CanSignIn);
    }

    [Fact]
    public void Failed_sign_ins_lock_the_account_for_the_lockout_period()
    {
        var account = CustomerAccount.Register("a@example.test", "Sample", null, "en", "hash", "1", false, Now).Value;
        for (var attempt = 1; attempt < 5; attempt++)
        {
            Assert.False(account.RecordFailedSignIn(5, TimeSpan.FromMinutes(15), Now));
        }

        Assert.True(account.RecordFailedSignIn(5, TimeSpan.FromMinutes(15), Now));
        Assert.True(account.IsLockedOut(Now.AddMinutes(14)));
        Assert.False(account.IsLockedOut(Now.AddMinutes(16)));
    }

    [Fact]
    public void Erasure_removes_personal_data_and_can_never_sign_in()
    {
        var account = CustomerAccount.Register("a@example.test", "Sample", "Sample Co", "en", "hash", "1", true, Now).Value;
        account.VerifyEmail(Now);
        account.Erase();

        Assert.Equal(CustomerAccountStatus.Erased, account.Status);
        Assert.DoesNotContain("a@example", account.Email, StringComparison.Ordinal);
        Assert.Null(account.Company);
        Assert.False(account.MarketingConsent);
        Assert.False(account.CanSignIn);
        Assert.True(account.VerifyEmail(Now).IsFailure);
        Assert.True(account.Enable().IsFailure);
    }

    [Fact]
    public void Tokens_are_single_use_and_expire()
    {
        var hash = new string('A', CustomerToken.HashLength);
        var token = CustomerToken.Issue(Guid.NewGuid(), CustomerTokenPurpose.EmailVerification, hash, TimeSpan.FromHours(1), Now);
        Assert.True(token.Use(Now.AddMinutes(59)).IsSuccess);
        Assert.True(token.Use(Now.AddMinutes(59)).IsFailure);

        var expired = CustomerToken.Issue(Guid.NewGuid(), CustomerTokenPurpose.PasswordReset, hash, TimeSpan.FromHours(1), Now);
        Assert.True(expired.Use(Now.AddHours(1)).IsFailure);
        Assert.Throws<ArgumentException>(() => CustomerToken.Issue(Guid.NewGuid(), CustomerTokenPurpose.PasswordReset, "short", TimeSpan.FromHours(1), Now));
    }

    [Fact]
    public void Sessions_end_when_idle_at_the_absolute_limit_or_when_revoked()
    {
        var hash = new string('B', CustomerSession.HashLength);
        var session = CustomerSession.Start(Guid.NewGuid(), hash, "stamp", TimeSpan.FromDays(7), TimeSpan.FromDays(30), "203.0.113.5", Now);
        Assert.True(session.IsActive(Now.AddDays(6)));
        Assert.False(session.IsActive(Now.AddDays(8)));

        for (var day = 6; day <= 30; day += 6)
        {
            session.Touch(TimeSpan.FromDays(7), Now.AddDays(day));
        }

        Assert.False(session.IsActive(Now.AddDays(30)));
        Assert.Equal(Now.AddDays(30), session.IdleExpiresAtUtc);

        var revoked = CustomerSession.Start(Guid.NewGuid(), hash, "stamp", TimeSpan.FromDays(7), TimeSpan.FromDays(30), null, Now);
        revoked.Revoke("SignOut", Now);
        Assert.False(revoked.IsActive(Now));
    }

    [Fact]
    public void Products_publish_only_with_a_verification_and_complete_copy_in_both_languages()
    {
        Assert.True(SaasProduct.Create("Bad Key!").IsFailure);
        var product = SaasProduct.Create("erp").Value;
        Assert.True(product.SetTranslation("ar", "نظام", null, "ملخص", "وصف").IsSuccess);
        Assert.Equal("نظام", product.Translation("ar")!.Slug);
        Assert.Equal("Product.Unverified", product.Publish(Now).Error!.Code);

        product.RecordVerification(Guid.NewGuid(), "Owner confirmation (sample)", Now);
        Assert.Equal("Product.IncompleteTranslation", product.Publish(Now).Error!.Code);

        product.SetTranslation("en", "System", "sample-system", "Summary", "Description");
        Assert.True(product.Publish(Now).IsSuccess);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.True(product.SetTranslation("en", "System", "sample-system", string.Empty, "Description").IsFailure);
    }

    [Fact]
    public void Demo_links_must_be_https_and_trials_bounded()
    {
        var product = SaasProduct.Create("crm").Value;
        Assert.True(product.Configure(14, DemoKind.Video, "http://example.test/demo", 0).IsFailure);
        Assert.True(product.Configure(14, DemoKind.Video, "https://user:secret@example.test/demo", 0).IsFailure);
        Assert.True(product.Configure(91, DemoKind.None, null, 0).IsFailure);
        Assert.True(product.Configure(14, DemoKind.Sandbox, "https://example.test/demo", 0).IsSuccess);
        Assert.True(product.Configure(null, DemoKind.AiAssistant, "https://ignored.example.test", 0).IsSuccess);
        Assert.Null(product.DemoUrl);
        Assert.False(product.OffersTrial);
    }

    [Fact]
    public void Plan_prices_are_shown_only_when_real_and_marked_public()
    {
        var product = SaasProduct.Create("crm").Value;
        var text = new Dictionary<string, (string?, string?)> { ["ar"] = ("أساسية", null), ["en"] = ("Basic", null) };
        Assert.True(product.AddPlan("basic", text, null, "SAR", BillingPeriod.Monthly, isPricePublic: true, 0).IsFailure);
        Assert.True(product.AddPlan("basic", text, 99.999m, "SAR", BillingPeriod.Monthly, isPricePublic: false, 0).IsFailure);
        Assert.True(product.AddPlan("basic", text, 99m, "SAR", BillingPeriod.Custom, isPricePublic: true, 0).IsFailure);
        Assert.True(product.AddPlan("basic", new Dictionary<string, (string?, string?)> { ["en"] = ("Basic", null) }, 99m, "SAR", BillingPeriod.Monthly, false, 0).IsFailure);

        var hidden = product.AddPlan("hidden", text, 99m, "sar", BillingPeriod.Monthly, isPricePublic: false, 0).Value;
        var shown = product.AddPlan("shown", text, 99m, "SAR", BillingPeriod.Yearly, isPricePublic: true, 1).Value;
        Assert.Equal("SAR", hidden.Currency);
        Assert.False(hidden.ShowsPrice);
        Assert.True(shown.ShowsPrice);
        Assert.True(product.AddPlan("shown", text, null, null, BillingPeriod.Monthly, false, 2).IsFailure);
    }

    [Fact]
    public void Subscriptions_follow_their_transitions_and_trials_expire_once()
    {
        Assert.True(CustomerSubscription.StartTrial(Guid.NewGuid(), Guid.NewGuid(), null, 0, Now).IsFailure);
        var trial = CustomerSubscription.StartTrial(Guid.NewGuid(), Guid.NewGuid(), null, 14, Now).Value;
        Assert.False(trial.ExpireTrial(Now.AddDays(13)));
        Assert.True(trial.ExpireTrial(Now.AddDays(14)));
        Assert.False(trial.ExpireTrial(Now.AddDays(15)));
        Assert.True(trial.Activate(null, null, Now).IsFailure);

        var request = CustomerSubscription.Request(Guid.NewGuid(), Guid.NewGuid(), null, "Sample note", Now).Value;
        Assert.True(request.Activate(DateOnly.FromDateTime(Now.UtcDateTime), null, Now).IsFailure);
        Assert.True(request.Activate(DateOnly.FromDateTime(Now.UtcDateTime.AddYears(1)), "ok", Now).IsSuccess);
        Assert.True(request.Reject(null, Now).IsFailure);
        Assert.True(request.Cancel(Now).IsSuccess);
        Assert.True(request.Cancel(Now).IsFailure);
        Assert.False(request.IsOpen);
    }

    [Fact]
    public void Invoices_change_only_as_drafts_and_follow_issue_paid_void()
    {
        var invoice = Invoice.Draft(Guid.NewGuid(), null, "sar", new DateOnly(2027, 1, 31), null).Value;
        Assert.Equal("SAR", invoice.Currency);
        Assert.Equal("Invoice.Empty", invoice.Issue("INV-1", "Sample", "a@example.test", Now).Error!.Code);
        Assert.True(invoice.ReplaceLines([("Sample", 0m, 10m)]).IsFailure);
        Assert.True(invoice.ReplaceLines([("Sample", 1m, 10.001m)]).IsFailure);

        Assert.True(invoice.ReplaceLines([("Setup", 1m, 1000m), ("Hours", 2.5m, 200m)]).IsSuccess);
        Assert.Equal(1500m, invoice.Total);
        Assert.False(invoice.IsVisibleToCustomer);
        Assert.True(invoice.MarkPaid(Now).IsFailure);

        Assert.True(invoice.Issue("INV-2026-00001", "Sample Co", "a@example.test", Now).IsSuccess);
        Assert.True(invoice.IsVisibleToCustomer);
        Assert.True(invoice.ReplaceLines([("Changed", 1m, 1m)]).IsFailure);
        Assert.True(invoice.MarkPaid(Now).IsSuccess);
        Assert.True(invoice.Void(Now).IsFailure);
    }

    [Fact]
    public void Demo_prompts_are_bounded_and_outputs_truncated()
    {
        Assert.True(AiDemoRun.Queue(Guid.NewGuid(), Guid.NewGuid(), "en", " ", Now).IsFailure);
        Assert.True(AiDemoRun.Queue(Guid.NewGuid(), Guid.NewGuid(), "en", new string('x', AiDemoRun.PromptMaxLength + 1), Now).IsFailure);
        var run = AiDemoRun.Queue(Guid.NewGuid(), Guid.NewGuid(), "en", "What does it do?", Now).Value;
        run.Complete(new string('y', AiDemoRun.OutputMaxLength + 50), Now);
        Assert.Equal(AiDemoRun.OutputMaxLength, run.Output!.Length);
        Assert.Equal(AiDemoRunStatus.Completed, run.Status);
    }
}
