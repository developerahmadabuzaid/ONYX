using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>Shared input rules for the delivery workspace: trimmed text, optional http(s) links, labels.</summary>
public static partial class DeliveryText
{
    public const int UrlMaxLength = 500;
    public const int LabelMaxLength = 40;
    public const int MaxLabels = 20;

    /// <summary>Trims; returns null for blank input; fails when longer than <paramref name="maxLength"/>.</summary>
    public static Result<string?> Optional(string? value, int maxLength, string code, string message)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return Result.Success<string?>(null);
        }

        return clean.Length <= maxLength ? clean : Error.Validation(code, message);
    }

    /// <summary>Trims and requires 1..<paramref name="maxLength"/> characters.</summary>
    public static Result<string> Required(string? value, int maxLength, string code, string message)
    {
        var clean = value?.Trim();
        return !string.IsNullOrEmpty(clean) && clean.Length <= maxLength ? clean : Error.Validation(code, message);
    }

    /// <summary>An optional absolute http(s) link (repository, branch page, pull request, commit). Never fetched by Onyx.</summary>
    public static Result<string?> OptionalUrl(string? value, string field)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return Result.Success<string?>(null);
        }

        return clean.Length <= UrlMaxLength
            && Uri.TryCreate(clean, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(uri.UserInfo)
                ? clean
                : Error.Validation("Delivery.InvalidUrl", $"{field} must be an http(s) address of at most {UrlMaxLength} characters.");
    }

    /// <summary>Labels: trimmed, lower-cased, distinct, no spaces (letters, digits, '-', '_', '.').</summary>
    public static Result<List<string>> Labels(IEnumerable<string>? labels)
    {
        var clean = (labels ?? [])
            .Select(label => label.Trim().ToLowerInvariant())
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (clean.Count > MaxLabels || clean.Any(label => label.Length > LabelMaxLength || !LabelPattern().IsMatch(label)))
        {
            return Error.Validation("Delivery.InvalidLabels", $"Up to {MaxLabels} labels of at most {LabelMaxLength} letters, digits, '-', '_' or '.' (no spaces).");
        }

        return clean;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}\-_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LabelPattern();
}
