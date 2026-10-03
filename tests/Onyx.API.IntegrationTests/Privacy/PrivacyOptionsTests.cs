using Microsoft.Extensions.Options;
using Onyx.Infrastructure.Privacy;

namespace Onyx.API.IntegrationTests.Privacy;

public sealed class PrivacyOptionsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("not base64!", false)]
    [InlineData("c2hvcnQ=", false)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", true)]
    public void The_IP_hash_key_is_optional_but_must_be_strong_when_set(string? key, bool valid) =>
        Assert.Equal(valid, PrivacyOptions.IsValidKey(key));

    [Fact]
    public void Without_a_key_no_IP_address_is_stored()
    {
        var hasher = new HmacIpAddressHasher(Options.Create(new PrivacyOptions()));

        Assert.Null(hasher.Hash("203.0.113.7"));
    }

    [Fact]
    public void With_a_key_the_IP_address_is_stored_only_as_a_keyed_hash()
    {
        var hasher = new HmacIpAddressHasher(Options.Create(new PrivacyOptions { IpHashKey = Convert.ToBase64String(new byte[32]) }));

        var hash = hasher.Hash("203.0.113.7");

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain("203.0.113.7", hash, StringComparison.Ordinal);
        Assert.Equal(hash, hasher.Hash("203.0.113.7"));
    }
}
