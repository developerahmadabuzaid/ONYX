using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Onyx.Infrastructure.Identity;

/// <summary>Issues short-lived HMAC-SHA256 access tokens with minimal claims (no permissions, no personal data).</summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options)
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(Guid userId, Guid sessionId, string securityStampHash, DateTimeOffset nowUtc)
    {
        var settings = options.Value;
        var expires = nowUtc.AddMinutes(settings.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = nowUtc.UtcDateTime,
            NotBefore = nowUtc.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [OnyxClaimTypes.Subject] = userId.ToString(),
                [OnyxClaimTypes.Session] = sessionId.ToString(),
                [OnyxClaimTypes.SecurityStampHash] = securityStampHash,
                [OnyxClaimTypes.TokenId] = Guid.NewGuid().ToString("N"),
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(settings.GetSigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        };

        return (_handler.CreateToken(descriptor), expires);
    }
}
