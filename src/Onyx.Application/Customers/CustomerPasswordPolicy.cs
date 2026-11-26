using Onyx.Domain.Common;

namespace Onyx.Application.Customers;

/// <summary>The same length-first policy as staff accounts: at least 12 characters with at least 4 different ones.</summary>
public static class CustomerPasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
    public const int MinUniqueChars = 4;

    public static Result Validate(string? password, string? email)
    {
        if (password is null || password.Length < MinLength || password.Length > MaxLength || password.Distinct().Count() < MinUniqueChars)
        {
            return Error.Validation("Customer.WeakPassword", $"Use {MinLength} to {MaxLength} characters with at least {MinUniqueChars} different characters.");
        }

        if (!string.IsNullOrEmpty(email) && password.Contains(email.Split('@')[0], StringComparison.OrdinalIgnoreCase) && email.Split('@')[0].Length >= 4)
        {
            return Error.Validation("Customer.WeakPassword", "The password must not contain your email address.");
        }

        return Result.Success();
    }
}
