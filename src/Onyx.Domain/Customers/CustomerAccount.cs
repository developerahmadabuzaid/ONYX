using System.Net.Mail;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Customers;

/// <summary>
/// A customer of Onyx's SaaS products. Deliberately separate from staff accounts: a customer has no roles or permissions and
/// signs in through its own scheme (docs/architecture.md §24.8). Registration needs accepted terms; sign-in needs a verified
/// email address. Personal fields are <see cref="SensitiveDataAttribute"/>: audit records only that they changed.
/// </summary>
public sealed class CustomerAccount : AuditableEntity, IAuditable
{
    public const int EmailMaxLength = 254;
    public const int DisplayNameMaxLength = 100;
    public const int CompanyMaxLength = 150;
    public const int TermsVersionMaxLength = 20;
    public const int PasswordHashMaxLength = 500;
    public const int SecurityStampLength = 32;

    private CustomerAccount()
    {
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        DisplayName = string.Empty;
        PreferredLanguage = string.Empty;
        PasswordHash = string.Empty;
        SecurityStamp = string.Empty;
        TermsVersion = string.Empty;
    }

    [SensitiveData]
    public string Email { get; private set; }

    [SensitiveData]
    public string NormalizedEmail { get; private set; }

    [SensitiveData]
    public string DisplayName { get; private set; }

    [SensitiveData]
    public string? Company { get; private set; }

    public string PreferredLanguage { get; private set; }

    [SensitiveData]
    public string PasswordHash { get; private set; }

    /// <summary>Changes whenever credentials change; sessions created before the change stop working.</summary>
    [SensitiveData]
    public string SecurityStamp { get; private set; }

    public CustomerAccountStatus Status { get; private set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    public string TermsVersion { get; private set; }

    public DateTimeOffset TermsAcceptedAtUtc { get; private set; }

    public bool MarketingConsent { get; private set; }

    public DateTimeOffset? MarketingConsentChangedAtUtc { get; private set; }

    [NotAudited]
    public int FailedSignInCount { get; private set; }

    [NotAudited]
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    [NotAudited]
    public DateTimeOffset? LastSignInAtUtc { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Customers;

    public bool CanSignIn => Status == CustomerAccountStatus.Active;

    public static Result<CustomerAccount> Register(
        string? email, string? displayName, string? company, string language, string passwordHash, string termsVersion, bool marketingConsent, DateTimeOffset nowUtc)
    {
        var cleanEmail = NormalizeEmailInput(email);
        if (cleanEmail.IsFailure)
        {
            return cleanEmail.Error!;
        }

        var account = new CustomerAccount
        {
            Email = cleanEmail.Value,
            NormalizedEmail = NormalizeEmail(cleanEmail.Value),
            PasswordHash = RequireHash(passwordHash),
            SecurityStamp = NewStamp(),
            Status = CustomerAccountStatus.PendingVerification,
            TermsVersion = string.IsNullOrWhiteSpace(termsVersion) || termsVersion.Length > TermsVersionMaxLength
                ? throw new ArgumentException("A terms version is required.", nameof(termsVersion))
                : termsVersion,
            TermsAcceptedAtUtc = nowUtc,
        };

        var profile = account.UpdateProfile(displayName, company, language);
        if (profile.IsFailure)
        {
            return profile.Error!;
        }

        account.SetMarketingConsent(marketingConsent, nowUtc);
        return account;
    }

    /// <summary>Upper-cased, trimmed lookup key (unique). Email local parts are treated case-insensitively, as every major provider does.</summary>
    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToUpperInvariant();
    }

    public static Result<string> NormalizeEmailInput(string? email)
    {
        var clean = email?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > EmailMaxLength || !MailAddress.TryCreate(clean, out var parsed)
            || !string.Equals(parsed.Address, clean, StringComparison.OrdinalIgnoreCase) || !clean.Contains('.', StringComparison.Ordinal))
        {
            return Error.Validation("Customer.InvalidEmail", "Enter a valid email address.");
        }

        return clean;
    }

    public Result UpdateProfile(string? displayName, string? company, string language)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > DisplayNameMaxLength)
        {
            return Error.Validation("Customer.InvalidName", $"Enter your name (at most {DisplayNameMaxLength} characters).");
        }

        var cleanCompany = company?.Trim();
        if (cleanCompany is { Length: > CompanyMaxLength })
        {
            return Error.Validation("Customer.InvalidCompany", $"The company name is at most {CompanyMaxLength} characters.");
        }

        if (language is not ("ar" or "en"))
        {
            return Error.Validation("Customer.InvalidLanguage", "Choose Arabic or English.");
        }

        DisplayName = name;
        Company = string.IsNullOrEmpty(cleanCompany) ? null : cleanCompany;
        PreferredLanguage = language;
        return Result.Success();
    }

    public void SetMarketingConsent(bool consent, DateTimeOffset nowUtc)
    {
        if (MarketingConsent == consent && MarketingConsentChangedAtUtc is not null)
        {
            return;
        }

        MarketingConsent = consent;
        MarketingConsentChangedAtUtc = nowUtc;
    }

    /// <summary>Idempotent: verifying an already verified address changes nothing.</summary>
    public Result VerifyEmail(DateTimeOffset nowUtc)
    {
        if (Status is CustomerAccountStatus.Disabled or CustomerAccountStatus.Erased)
        {
            return Error.Conflict("Customer.Unavailable", "This account is not available.");
        }

        EmailVerifiedAtUtc ??= nowUtc;
        Status = CustomerAccountStatus.Active;
        return Result.Success();
    }

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedUntilUtc is { } until && until > nowUtc;

    /// <summary>Counts a wrong password; returns true when this attempt locked the account.</summary>
    public bool RecordFailedSignIn(int maxAttempts, TimeSpan lockout, DateTimeOffset nowUtc)
    {
        FailedSignInCount++;
        if (FailedSignInCount < maxAttempts)
        {
            return false;
        }

        FailedSignInCount = 0;
        LockedUntilUtc = nowUtc.Add(lockout);
        return true;
    }

    public void RecordSignIn(DateTimeOffset nowUtc)
    {
        FailedSignInCount = 0;
        LockedUntilUtc = null;
        LastSignInAtUtc = nowUtc;
    }

    /// <summary>New password: the security stamp rotates, so every existing session ends.</summary>
    public void ChangePassword(string passwordHash)
    {
        PasswordHash = RequireHash(passwordHash);
        SecurityStamp = NewStamp();
        FailedSignInCount = 0;
        LockedUntilUtc = null;
    }

    public Result Disable()
    {
        if (Status == CustomerAccountStatus.Erased)
        {
            return Error.Conflict("Customer.Erased", "This account was deleted.");
        }

        Status = CustomerAccountStatus.Disabled;
        SecurityStamp = NewStamp();
        return Result.Success();
    }

    public Result Enable()
    {
        if (Status != CustomerAccountStatus.Disabled)
        {
            return Error.Conflict("Customer.NotDisabled", "Only a disabled account can be enabled.");
        }

        Status = EmailVerifiedAtUtc is null ? CustomerAccountStatus.PendingVerification : CustomerAccountStatus.Active;
        return Result.Success();
    }

    /// <summary>
    /// Erasure at the customer's request: personal data is replaced, credentials are destroyed and the account can never sign in
    /// again. Billing records keep their own copies of the details legally required on an invoice.
    /// </summary>
    public void Erase()
    {
        var placeholder = $"erased-{Id:N}@invalid.onyx";
        Email = placeholder;
        NormalizedEmail = NormalizeEmail(placeholder);
        DisplayName = "—";
        Company = null;
        PasswordHash = "!";
        SecurityStamp = NewStamp();
        MarketingConsent = false;
        Status = CustomerAccountStatus.Erased;
    }

    private static string RequireHash(string passwordHash) =>
        string.IsNullOrEmpty(passwordHash) || passwordHash.Length > PasswordHashMaxLength
            ? throw new ArgumentException("A password hash is required.", nameof(passwordHash))
            : passwordHash;

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
