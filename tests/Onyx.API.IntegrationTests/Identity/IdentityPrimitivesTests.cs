using System.Text;
using Onyx.Infrastructure.Identity;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>Pure tests of the cryptographic building blocks (no database).</summary>
public sealed class IdentityPrimitivesTests
{
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    public void Totp_matches_rfc_6238_test_vectors(long unixSeconds, string expected)
    {
        // RFC 6238 Appendix B (SHA-1 key "12345678901234567890"), truncated to 6 digits.
        var key = Encoding.ASCII.GetBytes("12345678901234567890");

        Assert.Equal(expected, Totp.Compute(key, unixSeconds / Totp.StepSeconds));
    }

    [Fact]
    public void Totp_accepts_adjacent_steps_and_rejects_replays_and_old_codes()
    {
        var key = Encoding.ASCII.GetBytes("12345678901234567890");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000_020);
        var step = now.ToUnixTimeSeconds() / Totp.StepSeconds;

        Assert.True(Totp.TryMatch(key, Totp.Compute(key, step - 1), now, null, out var matched));
        Assert.Equal(step - 1, matched);
        Assert.False(Totp.TryMatch(key, Totp.Compute(key, step - 1), now, lastUsedStep: step - 1, out _));
        Assert.False(Totp.TryMatch(key, Totp.Compute(key, step - 3), now, null, out _));
        Assert.False(Totp.TryMatch(key, "12345", now, null, out _));
        Assert.False(Totp.TryMatch(key, "abcdef", now, null, out _));
    }

    [Fact]
    public void Base32_decoding_matches_rfc_4648()
    {
        Assert.Equal("foobar", Encoding.ASCII.GetString(Totp.DecodeBase32("MZXW6YTBOI======")));
        Assert.Equal("foobar", Encoding.ASCII.GetString(Totp.DecodeBase32("mzxw 6ytb oi")));
    }

    [Fact]
    public void Sealed_successors_open_only_with_the_predecessor_token()
    {
        var predecessorId = Guid.NewGuid();
        var sealedValue = RefreshSuccessorSealer.Seal("predecessor-raw-token", predecessorId, "successor-raw-token");

        Assert.DoesNotContain("successor-raw-token", sealedValue, StringComparison.Ordinal);
        Assert.Equal("successor-raw-token", RefreshSuccessorSealer.TryOpen("predecessor-raw-token", predecessorId, sealedValue));
        Assert.Null(RefreshSuccessorSealer.TryOpen("another-token", predecessorId, sealedValue));
        Assert.Null(RefreshSuccessorSealer.TryOpen("predecessor-raw-token", Guid.NewGuid(), sealedValue));
        Assert.Null(RefreshSuccessorSealer.TryOpen("predecessor-raw-token", predecessorId, "not-base64!"));
    }

    [Fact]
    public void Opaque_tokens_are_random_and_hashes_are_stable()
    {
        var first = TokenHashing.NewToken();
        var second = TokenHashing.NewToken();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 43);
        Assert.Equal(TokenHashing.Hash(first), TokenHashing.Hash(first));
        Assert.Equal(64, TokenHashing.Hash(first).Length);
        Assert.NotEqual(TokenHashing.SecurityStampHash("a"), TokenHashing.SecurityStampHash("b"));
    }
}
