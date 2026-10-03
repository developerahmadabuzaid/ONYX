using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Runs a real password-hash verification for unknown or ineligible accounts so response timing does not reveal whether an
/// account exists. Uses its own hasher with the same options as Identity's (Identity's hasher is scoped; this is a singleton).
/// </summary>
public sealed class PasswordTimingEqualizer
{
    private readonly PasswordHasher<ApplicationUser> _hasher;
    private readonly ApplicationUser _dummyUser = new();
    private readonly string _dummyHash;

    public PasswordTimingEqualizer(IOptions<PasswordHasherOptions> options)
    {
        _hasher = new PasswordHasher<ApplicationUser>(options);
        _dummyHash = _hasher.HashPassword(_dummyUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));
    }

    public void Burn(string password) => _hasher.VerifyHashedPassword(_dummyUser, _dummyHash, password);
}
