using Onyx.Application.Customers;

namespace Onyx.Application.Tests.Customers;

/// <summary>Return paths carried through sign-in and email links can never become an open redirect.</summary>
public sealed class CustomerReturnPathTests
{
    [Theory]
    [InlineData("/en/subscribe?product=crm&intent=trial")]
    [InlineData("/ar/%D8%AD%D8%B3%D8%A7%D8%A8%D9%8A")]
    public void Local_paths_of_the_public_site_are_kept(string path) => Assert.Equal(path, CustomerReturnPath.Clean(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/en/")]
    [InlineData("//evil.example/en/")]
    [InlineData("/\\evil.example")]
    [InlineData("/admin/users")]
    [InlineData("/en/x\r\nLocation: https://evil.example")]
    [InlineData("javascript:alert(1)")]
    public void Anything_else_is_dropped(string? path) => Assert.Null(CustomerReturnPath.Clean(path));

    [Fact]
    public void Passwords_need_length_and_variety_and_must_not_contain_the_email_name()
    {
        Assert.True(CustomerPasswordPolicy.Validate("short", null).IsFailure);
        Assert.True(CustomerPasswordPolicy.Validate("aaaaaaaaaaaaaaa", null).IsFailure);
        Assert.True(CustomerPasswordPolicy.Validate("samplecustomer 2026!", "samplecustomer@example.test").IsFailure);
        Assert.True(CustomerPasswordPolicy.Validate("correct horse battery staple", "someone@example.test").IsSuccess);
    }
}
