using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Contracts.Identity;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Sessions and rotating refresh tokens (docs/architecture.md §25.1). Methods add changes to the unit of work; callers save,
/// except <see cref="RefreshAsync"/> and the revoke methods, which persist immediately.
/// </summary>
public sealed class SessionManager(
    ApplicationDbContext dbContext,
    JwtTokenIssuer tokenIssuer,
    SessionValidationCache validationCache,
    IOptions<SessionOptions> options,
    IRequestContext requestContext,
    TimeProvider timeProvider)
{
    public const string ReasonLogout = "Logout";
    public const string ReasonRefreshReuse = "RefreshTokenReuse";
    public const string ReasonPasswordChanged = "PasswordChanged";
    public const string ReasonPasswordReset = "PasswordReset";
    public const string ReasonUserDisabled = "UserDisabled";
    public const string ReasonSecurityChange = "SecurityChange";
    public const string ReasonAdministrator = "RevokedByAdministrator";
    public const string ReasonIdleExpired = "IdleExpired";
    public const string ReasonDataSubjectErasure = "DataSubjectErasure";

    public TokenPairResponse Start(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var now = timeProvider.GetUtcNow();
        var session = new UserSession
        {
            UserId = user.Id,
            CreatedAtUtc = now,
            LastRefreshedAtUtc = now,
            AbsoluteExpiresAtUtc = now.AddHours(options.Value.AbsoluteLifetimeHours),
            IpAddress = requestContext.IpAddress,
        };
        dbContext.UserSessions.Add(session);

        var (rawToken, refreshToken) = NewRefreshToken(session.Id, session.AbsoluteExpiresAtUtc, now);
        dbContext.RefreshTokens.Add(refreshToken);
        return Pair(user, session, rawToken, refreshToken, now);
    }

    public async Task<RefreshOutcome> RefreshAsync(string rawToken, CancellationToken cancellationToken, bool isRetry = false)
    {
        var now = timeProvider.GetUtcNow();
        var hash = TokenHashing.Hash(rawToken);
        var token = await dbContext.RefreshTokens.Include(candidate => candidate.Session)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (token?.Session is not { } session || session.RevokedAtUtc is not null || now >= session.AbsoluteExpiresAtUtc)
        {
            return new RefreshOutcome(RefreshOutcomeKind.Invalid);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is not { IsActive: true })
        {
            await RevokeSessionAsync(session.Id, ReasonUserDisabled, cancellationToken).ConfigureAwait(false);
            return new RefreshOutcome(RefreshOutcomeKind.Invalid);
        }

        if (token.ConsumedAtUtc is { } consumedAt)
        {
            return await HandleConsumedTokenAsync(token, session, user, rawToken, consumedAt, now, cancellationToken).ConfigureAwait(false);
        }

        if (now >= token.ExpiresAtUtc)
        {
            await RevokeSessionAsync(session.Id, ReasonIdleExpired, cancellationToken).ConfigureAwait(false);
            return new RefreshOutcome(RefreshOutcomeKind.Invalid);
        }

        // Atomic claim: if a concurrent request consumed it first, re-evaluate through the grace path.
        var claimed = await dbContext.RefreshTokens
            .Where(candidate => candidate.Id == token.Id && candidate.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.ConsumedAtUtc, now), cancellationToken)
            .ConfigureAwait(false);
        if (claimed == 0)
        {
            dbContext.ChangeTracker.Clear();
            return isRetry ? new RefreshOutcome(RefreshOutcomeKind.Invalid) : await RefreshAsync(rawToken, cancellationToken, isRetry: true).ConfigureAwait(false);
        }

        var (successorRaw, successor) = NewRefreshToken(session.Id, session.AbsoluteExpiresAtUtc, now);
        dbContext.RefreshTokens.Add(successor);
        token.ConsumedAtUtc = now;
        token.ReplacedByTokenId = successor.Id;
        token.SealedSuccessor = RefreshSuccessorSealer.Seal(rawToken, token.Id, successorRaw);
        session.LastRefreshedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RefreshOutcome(RefreshOutcomeKind.Succeeded, Pair(user, session, successorRaw, successor, now), user.Id, session.Id);
    }

    public async Task<Guid?> RevokeByRefreshTokenAsync(string rawToken, string reason, CancellationToken cancellationToken)
    {
        var hash = TokenHashing.Hash(rawToken);
        var sessionId = await dbContext.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.SessionId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sessionId is { } id)
        {
            await RevokeSessionAsync(id, reason, cancellationToken).ConfigureAwait(false);
        }

        return sessionId;
    }

    public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await dbContext.UserSessions
            .Where(session => session.Id == sessionId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now).SetProperty(s => s.RevocationReason, reason), cancellationToken)
            .ConfigureAwait(false);
        validationCache.Remove([sessionId]);
    }

    public async Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessionIds = await dbContext.UserSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now).SetProperty(s => s.RevocationReason, reason), cancellationToken)
            .ConfigureAwait(false);

        validationCache.Remove(sessionIds);
        return sessionIds.Count;
    }

    private async Task<RefreshOutcome> HandleConsumedTokenAsync(
        RefreshToken token, UserSession session, ApplicationUser user, string rawToken, DateTimeOffset consumedAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var withinGrace = now - consumedAt <= TimeSpan.FromSeconds(settings.RefreshGraceSeconds) && token.GraceRedemptions < settings.MaxGraceRedemptions;

        if (withinGrace && token.SealedSuccessor is { } sealedSuccessor && token.ReplacedByTokenId is { } successorId
            && RefreshSuccessorSealer.TryOpen(rawToken, token.Id, sealedSuccessor) is { } successorRaw)
        {
            var successor = await dbContext.RefreshTokens.SingleAsync(candidate => candidate.Id == successorId, cancellationToken).ConfigureAwait(false);
            token.GraceRedemptions++;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new RefreshOutcome(RefreshOutcomeKind.Succeeded, Pair(user, session, successorRaw, successor, now), user.Id, session.Id);
        }

        // A consumed token presented again outside the grace window: assume theft and end the whole session.
        await RevokeSessionAsync(session.Id, ReasonRefreshReuse, cancellationToken).ConfigureAwait(false);
        return new RefreshOutcome(RefreshOutcomeKind.ReuseDetected, UserId: user.Id, SessionId: session.Id);
    }

    private (string RawToken, RefreshToken Token) NewRefreshToken(Guid sessionId, DateTimeOffset sessionExpiresAt, DateTimeOffset now)
    {
        var raw = TokenHashing.NewToken();
        var idleExpiry = now.AddMinutes(options.Value.IdleTimeoutMinutes);
        return (raw, new RefreshToken
        {
            SessionId = sessionId,
            TokenHash = TokenHashing.Hash(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = idleExpiry < sessionExpiresAt ? idleExpiry : sessionExpiresAt,
        });
    }

    private TokenPairResponse Pair(ApplicationUser user, UserSession session, string rawRefreshToken, RefreshToken refreshToken, DateTimeOffset now)
    {
        var (accessToken, accessExpires) = tokenIssuer.Issue(user.Id, session.Id, TokenHashing.SecurityStampHash(user.SecurityStamp), now);
        return new TokenPairResponse(accessToken, accessExpires, rawRefreshToken, refreshToken.ExpiresAtUtc, session.AbsoluteExpiresAtUtc);
    }
}
