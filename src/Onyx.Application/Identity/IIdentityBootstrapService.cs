using Onyx.Domain.Common;

namespace Onyx.Application.Identity;

/// <summary>Operator commands run from the command line, never over HTTP.</summary>
public interface IIdentityBootstrapService
{
    /// <summary>Creates missing seeded roles with their default permission bundles. Never overwrites roles that already exist.</summary>
    Task<int> SeedRolesAsync(CancellationToken cancellationToken);

    /// <summary>Creates the first SuperAdmin. Refuses if any SuperAdmin already exists. No default password exists anywhere.</summary>
    Task<Result<Guid>> CreateInitialSuperAdminAsync(string email, string displayName, string password, string preferredLanguage, CancellationToken cancellationToken);
}
