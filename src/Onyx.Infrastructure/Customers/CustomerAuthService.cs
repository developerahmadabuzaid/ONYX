using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Application.Auditing;
using Onyx.Application.Customers;
using Onyx.Application.Email;
using Onyx.Contracts.Customers;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Customers;
using Onyx.Domain.Subscriptions;
using Onyx.Infrastructure.Identity;
using Onyx.Infrastructure.Persistence;
using Onyx.Infrastructure.Seo;

namespace Onyx.Infrastructure.Customers;

/// <summary>
/// Customer identity (docs/architecture.md §24.8), deliberately separate from the staff <see cref="AuthService"/>: its own table,
/// the Identity password hasher, single-use hashed email tokens and opaque server-side sessions. Answers never reveal whether an
/// email address has an account, and wrong passwords cost the same time as unknown addresses.
/// </summary>
public sealed class CustomerAuthService(
    ApplicationDbContext dbContext,
    IPasswordHasher<CustomerAccount> passwordHasher,
    ICaptchaVerifier captcha,
    IEmailOutbox emailOutbox,
    IRequestContext requestContext,
    IAuditTrail auditTrail,
    CustomerAccountOptions options,
    IOptions<PublicSiteOptions> site,
    TimeProvider timeProvider) : ICustomerAuthService
{
    public const string RegisterCaptchaAction = "customer-register";
    public const string SignInCaptchaAction = "customer-sign-in";
    public const string RecoveryCaptchaAction = "customer-recovery";

    private const string ReasonSignOut = "SignOut";

    private static readonly Error InvalidCredentials = Error.Unauthorized(
        "Customer.InvalidCredentials", "The email or password is incorrect, or there were too many attempts. Try again later or reset your password.");
    private static readonly Error InvalidLink = Error.Validation("Customer.InvalidToken", "This link is invalid or has expired.");
    private static readonly Error LinksUnavailable = Error.Unavailable("Customer.Unavailable", "Customer accounts are not available yet.");

    // A fixed hash to verify against when the address is unknown, so both paths take the same time.
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<CustomerAccount>().HashPassword(null!, Guid.NewGuid().ToString("N")));

    public async Task<Result> RegisterAsync(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (site.Value.Origin is null)
        {
            return LinksUnavailable;
        }

        if (!request.AcceptTerms)
        {
            return Error.Validation("Customer.TermsRequired", "Accept the terms and the privacy policy to create an account.");
        }

        var email = CustomerAccount.NormalizeEmailInput(request.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var password = CustomerPasswordPolicy.Validate(request.Password, email.Value);
        if (password.IsFailure)
        {
            return password;
        }

        var verified = await captcha.VerifyAsync(request.CaptchaToken, RegisterCaptchaAction, requestContext.IpAddress, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified;
        }

        var now = timeProvider.GetUtcNow();
        var language = request.Language == "en" ? "en" : "ar";
        var returnPath = CustomerReturnPath.Clean(request.ReturnPath);
        var existing = await FindByEmailAsync(email.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            // Same work and the same answer as a new sign-up; the owner of the address is told instead.
            _ = passwordHasher.HashPassword(existing, request.Password);
            if (existing.Status == CustomerAccountStatus.PendingVerification)
            {
                await QueueVerificationAsync(existing, returnPath, now, cancellationToken).ConfigureAwait(false);
            }
            else if (existing.Status == CustomerAccountStatus.Active)
            {
                await QueueAsync(existing, CustomerEmailTemplates.AlreadyRegistered, new Dictionary<string, string>
                {
                    ["name"] = existing.DisplayName,
                    ["link"] = Link(CustomerRouteSegments.SignIn, existing.PreferredLanguage, null),
                }, cancellationToken).ConfigureAwait(false);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        var registered = CustomerAccount.Register(
            email.Value, request.DisplayName, request.Company, language, "pending", options.TermsVersion, request.MarketingConsent, now);
        if (registered.IsFailure)
        {
            return registered.Error!;
        }

        var account = registered.Value;
        account.ChangePassword(passwordHasher.HashPassword(account, request.Password));
        dbContext.CustomerAccounts.Add(account);
        await QueueVerificationAsync(account, returnPath, now, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.CustomerRegistered, AuditCategory.Customers, nameof(CustomerAccount), account.Id.ToString(),
            new { language, marketingConsent = request.MarketingConsent, termsVersion = options.TermsVersion });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent sign-up with the same address won the unique index; answer exactly as for an existing account.
            dbContext.ChangeTracker.Clear();
        }

        return Result.Success();
    }

    public async Task<Result> VerifyEmailAsync(VerifyCustomerEmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var token = await FindTokenAsync(request.CustomerId, request.Token, CustomerTokenPurpose.EmailVerification, cancellationToken).ConfigureAwait(false);
        if (token is null || token.Use(now).IsFailure)
        {
            return InvalidLink;
        }

        var account = await dbContext.CustomerAccounts.SingleAsync(candidate => candidate.Id == token.CustomerId, cancellationToken).ConfigureAwait(false);
        var wasPending = account.Status == CustomerAccountStatus.PendingVerification;
        var verified = account.VerifyEmail(now);
        if (verified.IsFailure)
        {
            return InvalidLink;
        }

        if (wasPending)
        {
            auditTrail.Record(AuditAction.CustomerEmailVerified, AuditCategory.Customers, nameof(CustomerAccount), account.Id.ToString());
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ResendVerificationAsync(ResendCustomerVerificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (site.Value.Origin is null)
        {
            return LinksUnavailable;
        }

        var verified = await captcha.VerifyAsync(request.CaptchaToken, RecoveryCaptchaAction, requestContext.IpAddress, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified;
        }

        var email = CustomerAccount.NormalizeEmailInput(request.Email);
        if (email.IsSuccess && await FindByEmailAsync(email.Value, cancellationToken).ConfigureAwait(false) is { Status: CustomerAccountStatus.PendingVerification } account)
        {
            await QueueVerificationAsync(account, CustomerReturnPath.Clean(request.ReturnPath), timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result<CustomerSessionResponse>> SignInAsync(CustomerSignInRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var verified = await captcha.VerifyAsync(request.CaptchaToken, SignInCaptchaAction, requestContext.IpAddress, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        var now = timeProvider.GetUtcNow();
        var email = CustomerAccount.NormalizeEmailInput(request.Email);
        var account = email.IsSuccess ? await FindByEmailAsync(email.Value, cancellationToken).ConfigureAwait(false) : null;
        if (account is null || account.Status == CustomerAccountStatus.Erased)
        {
            passwordHasher.VerifyHashedPassword(null!, DummyHash.Value, request.Password ?? string.Empty);
            return InvalidCredentials;
        }

        if (account.IsLockedOut(now))
        {
            passwordHasher.VerifyHashedPassword(account, DummyHash.Value, request.Password ?? string.Empty);
            // Same answer as a wrong password, so a lockout does not reveal that the address has an account.
            return InvalidCredentials;
        }

        var check = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password ?? string.Empty);
        if (check == PasswordVerificationResult.Failed)
        {
            if (account.RecordFailedSignIn(options.MaxFailedSignIns, TimeSpan.FromMinutes(options.LockoutMinutes), now))
            {
                auditTrail.Record(AuditAction.CustomerLockedOut, AuditCategory.Customers, nameof(CustomerAccount), account.Id.ToString());
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return InvalidCredentials;
        }

        // The password is right: now (and only now) the account state may be revealed.
        if (account.Status == CustomerAccountStatus.PendingVerification)
        {
            return Error.Forbidden("Customer.EmailNotVerified", "Verify your email address first. We can send the link again.");
        }

        if (!account.CanSignIn)
        {
            return Error.Forbidden("Customer.Disabled", "This account is disabled. Contact Onyx for help.");
        }

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.ChangePassword(passwordHasher.HashPassword(account, request.Password!));
        }

        account.RecordSignIn(now);
        var raw = TokenHashing.NewToken();
        var session = CustomerSession.Start(account.Id, TokenHashing.Hash(raw), account.SecurityStamp,
            TimeSpan.FromDays(options.SessionIdleDays), TimeSpan.FromDays(options.SessionAbsoluteDays), requestContext.IpAddress, now);
        dbContext.CustomerSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CustomerSessionResponse(raw, session.AbsoluteExpiresAtUtc, account.Id, account.DisplayName, account.PreferredLanguage);
    }

    public async Task SignOutAsync(string sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return;
        }

        var hash = TokenHashing.Hash(sessionToken);
        var session = await dbContext.CustomerSessions.SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken).ConfigureAwait(false);
        if (session is not null)
        {
            session.Revoke(ReasonSignOut, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Result> RequestPasswordResetAsync(CustomerForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (site.Value.Origin is null)
        {
            return LinksUnavailable;
        }

        var verified = await captcha.VerifyAsync(request.CaptchaToken, RecoveryCaptchaAction, requestContext.IpAddress, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified;
        }

        var email = CustomerAccount.NormalizeEmailInput(request.Email);
        if (email.IsSuccess && await FindByEmailAsync(email.Value, cancellationToken).ConfigureAwait(false) is { Status: CustomerAccountStatus.Active } account)
        {
            var raw = Issue(account.Id, CustomerTokenPurpose.PasswordReset, TimeSpan.FromMinutes(options.PasswordResetLifetimeMinutes), timeProvider.GetUtcNow());
            await QueueAsync(account, CustomerEmailTemplates.PasswordReset, new Dictionary<string, string>
            {
                ["name"] = account.DisplayName,
                ["link"] = Link(CustomerRouteSegments.ResetPassword, account.PreferredLanguage, null, account.Id, raw),
                ["minutes"] = options.PasswordResetLifetimeMinutes.ToString(CultureInfo.InvariantCulture),
            }, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(CustomerResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = timeProvider.GetUtcNow();
        var token = await FindTokenAsync(request.CustomerId, request.Token, CustomerTokenPurpose.PasswordReset, cancellationToken).ConfigureAwait(false);
        if (token is null || !token.IsUsable(now))
        {
            return InvalidLink;
        }

        var account = await dbContext.CustomerAccounts.SingleAsync(candidate => candidate.Id == token.CustomerId, cancellationToken).ConfigureAwait(false);
        if (account.Status != CustomerAccountStatus.Active)
        {
            return InvalidLink;
        }

        var password = CustomerPasswordPolicy.Validate(request.NewPassword, account.Email);
        if (password.IsFailure)
        {
            return password;
        }

        token.Use(now);
        account.ChangePassword(passwordHasher.HashPassword(account, request.NewPassword));
        await RevokeSessionsAsync(account.Id, "PasswordReset", now, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.CustomerPasswordReset, AuditCategory.Customers, nameof(CustomerAccount), account.Id.ToString());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid customerId, CustomerChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await dbContext.CustomerAccounts.SingleOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken).ConfigureAwait(false);
        if (account is not { CanSignIn: true })
        {
            return Error.Unauthorized("Customer.NotSignedIn", "Sign in to continue.");
        }

        if (passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.CurrentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return Error.Validation("Customer.WrongPassword", "The current password is incorrect.");
        }

        var password = CustomerPasswordPolicy.Validate(request.NewPassword, account.Email);
        if (password.IsFailure)
        {
            return password;
        }

        var now = timeProvider.GetUtcNow();
        account.ChangePassword(passwordHasher.HashPassword(account, request.NewPassword));
        await RevokeSessionsAsync(account.Id, "PasswordChanged", now, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.CustomerPasswordChanged, AuditCategory.Customers, nameof(CustomerAccount), account.Id.ToString());
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> DeleteAccountAsync(Guid customerId, DeleteCustomerAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await dbContext.CustomerAccounts.SingleOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken).ConfigureAwait(false);
        if (account is not { CanSignIn: true })
        {
            return Error.Unauthorized("Customer.NotSignedIn", "Sign in to continue.");
        }

        if (passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return Error.Validation("Customer.WrongPassword", "The password is incorrect.");
        }

        var now = timeProvider.GetUtcNow();
        var open = await dbContext.CustomerSubscriptions
            .Where(subscription => subscription.CustomerId == customerId
                && (subscription.Status == SubscriptionStatus.Requested || subscription.Status == SubscriptionStatus.Trial || subscription.Status == SubscriptionStatus.Active))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var subscription in open)
        {
            subscription.Cancel(now);
        }

        account.Erase();
        await RevokeSessionsAsync(account.Id, "AccountDeleted", now, cancellationToken).ConfigureAwait(false);
        await dbContext.CustomerTokens.Where(token => token.CustomerId == customerId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AiDemoRuns.Where(run => run.CustomerId == customerId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.CustomerAccountDeleted, AuditCategory.Privacy, nameof(CustomerAccount), account.Id.ToString(), new { cancelledSubscriptions = open.Count });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<CustomerSessionInfo?> ValidateSessionAsync(string sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionToken) || sessionToken.Length > 100)
        {
            return null;
        }

        var hash = TokenHashing.Hash(sessionToken);
        var now = timeProvider.GetUtcNow();
        var row = await (from session in dbContext.CustomerSessions
                         join account in dbContext.CustomerAccounts on session.CustomerId equals account.Id
                         where session.TokenHash == hash
                         select new { Session = session, account.Status, account.SecurityStamp })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // A credential change, disable or erasure rotates the account's stamp, which ends every older session at once.
        if (row is null || !row.Session.IsActive(now) || row.Status != CustomerAccountStatus.Active
            || !string.Equals(row.Session.SecurityStamp, row.SecurityStamp, StringComparison.Ordinal))
        {
            return null;
        }

        // Slide the idle window at most once a minute, so ordinary page views do not write on every request.
        if (now - row.Session.LastSeenAtUtc >= TimeSpan.FromMinutes(1))
        {
            row.Session.Touch(TimeSpan.FromDays(options.SessionIdleDays), now);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new CustomerSessionInfo(row.Session.CustomerId, row.Session.Id);
    }

    private Task<CustomerAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = CustomerAccount.NormalizeEmail(email);
        return dbContext.CustomerAccounts.SingleOrDefaultAsync(account => account.NormalizedEmail == normalized, cancellationToken);
    }

    private Task<CustomerToken?> FindTokenAsync(Guid customerId, string? rawToken, CustomerTokenPurpose purpose, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > 100)
        {
            return Task.FromResult<CustomerToken?>(null);
        }

        var hash = TokenHashing.Hash(rawToken);
        return dbContext.CustomerTokens.SingleOrDefaultAsync(
            token => token.TokenHash == hash && token.CustomerId == customerId && token.Purpose == purpose, cancellationToken);
    }

    private string Issue(Guid customerId, CustomerTokenPurpose purpose, TimeSpan lifetime, DateTimeOffset now)
    {
        var raw = TokenHashing.NewToken();
        dbContext.CustomerTokens.Add(CustomerToken.Issue(customerId, purpose, TokenHashing.Hash(raw), lifetime, now));
        return raw;
    }

    private Task QueueVerificationAsync(CustomerAccount account, string? returnPath, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var raw = Issue(account.Id, CustomerTokenPurpose.EmailVerification, TimeSpan.FromHours(options.VerificationLifetimeHours), now);
        return QueueAsync(account, CustomerEmailTemplates.VerifyEmail, new Dictionary<string, string>
        {
            ["name"] = account.DisplayName,
            ["link"] = Link(CustomerRouteSegments.VerifyEmail, account.PreferredLanguage, returnPath, account.Id, raw),
            ["hours"] = options.VerificationLifetimeHours.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
    }

    private async Task QueueAsync(CustomerAccount account, string template, Dictionary<string, string> model, CancellationToken cancellationToken) =>
        await emailOutbox.QueueAsync(new EmailRequest(template, account.PreferredLanguage, account.Email, account.DisplayName, model), cancellationToken)
            .ConfigureAwait(false);

    private async Task RevokeSessionsAsync(Guid customerId, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.CustomerSessions.Where(session => session.CustomerId == customerId && session.RevokedAtUtc == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var session in sessions)
        {
            session.Revoke(reason, now);
        }
    }

    private string Link(IReadOnlyDictionary<string, string> segments, string language, string? returnPath, Guid? customerId = null, string? token = null)
    {
        var origin = site.Value.Origin!.GetLeftPart(UriPartial.Authority);
        var query = new List<string>();
        if (customerId is { } id)
        {
            query.Add(FormattableString.Invariant($"id={id:D}"));
        }

        if (token is not null)
        {
            query.Add("token=" + Uri.EscapeDataString(token));
        }

        if (returnPath is not null)
        {
            query.Add("returnUrl=" + Uri.EscapeDataString(returnPath));
        }

        var path = "/" + language + "/" + Uri.EscapeDataString(segments[language]);
        return origin + path + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
    }
}
