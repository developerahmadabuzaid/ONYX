using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Identity;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Per-request check behind every access token. Lockout from failed logins deliberately does not end existing sessions,
/// so an attacker cannot sign a real admin out by guessing passwords.
/// </summary>
public sealed class SessionValidator(
    ApplicationDbContext dbContext,
    SessionValidationCache cache,
    IOptions<SessionOptions> options,
    TimeProvider timeProvider) : ISessionValidator
{
    public async Task<bool> IsValidAsync(Guid userId, Guid sessionId, string securityStampHash, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!cache.TryGet(sessionId, out var entry) || entry.UserId != userId)
        {
            var row = await dbContext.UserSessions.AsNoTracking()
                .Where(session => session.Id == sessionId && session.UserId == userId)
                .Join(dbContext.Users, session => session.UserId, user => user.Id, (session, user) => new
                {
                    session.RevokedAtUtc,
                    session.AbsoluteExpiresAtUtc,
                    user.IsActive,
                    user.SecurityStamp,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                return false;
            }

            entry = new SessionValidationEntry(
                userId,
                row.RevokedAtUtc is null && row.IsActive,
                TokenHashing.SecurityStampHash(row.SecurityStamp),
                row.AbsoluteExpiresAtUtc);

            if (options.Value.ValidationCacheSeconds > 0)
            {
                cache.Set(sessionId, entry, TimeSpan.FromSeconds(options.Value.ValidationCacheSeconds));
            }
        }

        return entry.IsUsable
            && now < entry.AbsoluteExpiresAtUtc
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(entry.SecurityStampHash), Encoding.ASCII.GetBytes(securityStampHash));
    }
}
