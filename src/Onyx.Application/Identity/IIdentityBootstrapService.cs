using Onyx.Domain.Common;

namespace Onyx.Application.Identity;

/// <summary>Operator commands run from the command line, never over HTTP.</summary>
public interface IIdentityBootstrapService
{
    /// <summary>Creates missing seeded roles with their default permission bundles. Never overwrites roles that already exist.</summary>
    Task<int> SeedRolesAsync(CancellationToken cancellationToken);

    /// <summary>Creates the first SuperAdmin. Refuses if any SuperAdmin already exists. No default password exists anywhere.</summary>
    Task<Result<Guid>> CreateInitialSuperAdminAsync(string email, string displayName, string password, string preferredLanguage, CancellationToken cancellationToken);

    /// <summary>
    /// Break-glass recovery when a SuperAdmin has lost their authenticator and no other enrolled SuperAdmin can reset it through
    /// the admin API. Removes every second-factor artefact, ends all sessions and sign-in steps, rotates the security stamp and
    /// writes a security audit record. The password is unchanged; the user must enrol a new authenticator at next sign-in.
    /// </summary>
    Task<Result<TwoFactorRecoveryResult>> RecoverSuperAdminTwoFactorAsync(string email, string reason, string operatorIdentity, CancellationToken cancellationToken);
}
