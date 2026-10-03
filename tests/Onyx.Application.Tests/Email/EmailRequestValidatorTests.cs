using Onyx.Application.Email;

namespace Onyx.Application.Tests.Email;

public sealed class EmailRequestValidatorTests
{
    private static readonly EmailRequestValidator Validator = new();

    private static EmailRequest Valid() =>
        new("system.provider-test", "ar", "client@example.com", "عميل", new Dictionary<string, string>());

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(Validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Invalid_addresses_fail(string address)
    {
        Assert.False(Validator.Validate(Valid() with { ToAddress = address }).IsValid);
    }

    [Theory]
    [InlineData("Name\r\nBcc: attacker@example.com")]
    [InlineData("Name\nX-Injected: 1")]
    public void Line_breaks_in_the_recipient_name_fail(string name)
    {
        Assert.False(Validator.Validate(Valid() with { ToName = name }).IsValid);
    }

    [Theory]
    [InlineData("arabic")]
    [InlineData("AR")]
    [InlineData("")]
    public void Language_codes_must_be_two_lowercase_letters(string language)
    {
        Assert.False(Validator.Validate(Valid() with { LanguageCode = language }).IsValid);
    }

    [Fact]
    public void Oversized_models_fail()
    {
        var model = Enumerable.Range(0, EmailRequestValidator.MaxModelEntries + 1).ToDictionary(i => $"k{i}", _ => "v");

        Assert.False(Validator.Validate(Valid() with { Model = model }).IsValid);
    }
}
