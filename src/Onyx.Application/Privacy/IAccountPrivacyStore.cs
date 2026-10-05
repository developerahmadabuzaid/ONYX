using Onyx.Domain.Common;

namespace Onyx.Application.Privacy;

/// <summary>Data-subject operations on an admin account (identity data, deferred here from Stage 3).</summary>
public interface IAccountPrivacyStore
{
    /// <summary>The account with this normalized email, or null when there is none.</summary>
    Task<AccountExport?> ExportAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>
    /// Anonymizes the account (disabled; identifying fields replaced; password, authenticator, recovery codes and roles removed;
    /// sessions revoked and their IPs cleared; refresh tokens and sign-in challenges deleted; the account's name removed from the
    /// audit log). Refused for the operator's own account and for the last active SuperAdmin. Returns the account id, or null
    /// when the subject has no account. Joins the caller's transaction.
    /// </summary>
    Task<Result<Guid?>> EraseAsync(string normalizedEmail, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}
