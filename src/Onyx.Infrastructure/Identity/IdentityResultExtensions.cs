using Microsoft.AspNetCore.Identity;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Identity;

internal static class IdentityResultExtensions
{
    public static Error ToValidationError(this IdentityResult result, string field = "password")
    {
        ArgumentNullException.ThrowIfNull(result);
        var messages = result.Errors.Select(error => error.Description).Distinct().ToArray();
        return Error.Validation("Identity.Invalid", "The request does not meet the account policy.", new Dictionary<string, string[]> { [field] = messages });
    }

    public static bool HasInvalidToken(this IdentityResult result) =>
        result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken));
}
