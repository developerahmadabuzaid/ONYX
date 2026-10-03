using FluentValidation;
using FluentValidation.Results;
using Onyx.Domain.Common;

namespace Onyx.Application.Common;

public static class ValidationExtensions
{
    public static async Task<Error?> ValidateToErrorAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsValid ? null : result.ToError();
    }

    public static Error ToError(this ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var errors = result.Errors
            .GroupBy(failure => string.IsNullOrEmpty(failure.PropertyName) ? "request" : failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(), StringComparer.Ordinal);
        return Error.Validation("Validation.Failed", "One or more fields are invalid.", errors);
    }
}
