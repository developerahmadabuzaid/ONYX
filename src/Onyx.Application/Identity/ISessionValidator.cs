namespace Onyx.Application.Identity;

/// <summary>
/// Checked on every authenticated API request: the session is not revoked or expired, the user is active and not locked out,
/// and the security stamp has not changed since the token was issued.
/// </summary>
public interface ISessionValidator
{
    Task<bool> IsValidAsync(Guid userId, Guid sessionId, string securityStampHash, CancellationToken cancellationToken);
}
