using Onyx.Domain.Common;

namespace Onyx.Application.Delivery;

/// <summary>Parses enum names sent by clients (case-insensitive, names only, never numbers).</summary>
public static class DeliveryEnums
{
    public static Result<T> Parse<T>(string? value, string field)
        where T : struct, Enum
    {
        var clean = value?.Trim();
        if (!string.IsNullOrEmpty(clean) && !clean.Any(char.IsDigit) && Enum.TryParse<T>(clean, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        return Error.Validation("Delivery.InvalidValue", $"{field} must be one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    /// <summary>Like <see cref="Parse{T}"/>, but blank input yields <paramref name="fallback"/>.</summary>
    public static Result<T> ParseOrDefault<T>(string? value, string field, T fallback)
        where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? Result.Success(fallback) : Parse<T>(value, field);
}
