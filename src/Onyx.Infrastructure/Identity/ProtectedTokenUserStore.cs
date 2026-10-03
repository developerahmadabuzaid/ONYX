using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Identity user store that encrypts the authenticator (TOTP) key and recovery codes at rest with Data Protection.
/// Identity stores them in plain text by default; a database leak alone must not yield second factors.
/// </summary>
public sealed class ProtectedTokenUserStore(ApplicationDbContext context, IdentityErrorDescriber describer, IDataProtectionProvider dataProtection)
    : UserStore<ApplicationUser, ApplicationRole, ApplicationDbContext, Guid>(context, describer)
{
    private const string IdentityLoginProvider = "[AspNetUserStore]";
    private static readonly HashSet<string> ProtectedTokenNames = new(StringComparer.Ordinal) { "AuthenticatorKey", "RecoveryCodes" };

    private readonly IDataProtector _protector = dataProtection.CreateProtector("Onyx.Identity.UserTokens.v1");

    public override Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
    {
        var stored = value is not null && IsProtected(loginProvider, name) ? _protector.Protect(value) : value;
        return base.SetTokenAsync(user, loginProvider, name, stored, cancellationToken);
    }

    public override async Task<string?> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        var stored = await base.GetTokenAsync(user, loginProvider, name, cancellationToken).ConfigureAwait(false);
        return stored is not null && IsProtected(loginProvider, name) ? _protector.Unprotect(stored) : stored;
    }

    private static bool IsProtected(string loginProvider, string name) =>
        loginProvider == IdentityLoginProvider && ProtectedTokenNames.Contains(name);
}
