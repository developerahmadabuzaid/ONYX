using Onyx.Domain.Common;

namespace Onyx.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("Service.NotFound", "Service was not found.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_carries_typed_error()
    {
        var result = Result.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("Service.NotFound", result.Error.Code);
    }

    [Fact]
    public void Generic_success_exposes_value_and_implicit_conversions_work()
    {
        Result<int> fromValue = 42;
        Result<int> fromError = NotFound;

        Assert.Equal(42, fromValue.Value);
        Assert.True(fromError.IsFailure);
    }

    [Fact]
    public void Reading_value_of_failure_throws()
    {
        var result = Result.Failure<string>(NotFound);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("Service.NotFound", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_error_keeps_field_errors()
    {
        var errors = new Dictionary<string, string[]> { ["Slug"] = ["Slug is required."] };

        var error = Error.Validation("Service.Invalid", "Validation failed.", errors);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Slug is required.", error.ValidationErrors!["Slug"][0]);
    }

    [Theory]
    [InlineData("", "message")]
    [InlineData("code", " ")]
    public void Error_requires_code_and_message(string code, string message)
    {
        Assert.ThrowsAny<ArgumentException>(() => Error.Failure(code, message));
    }
}
