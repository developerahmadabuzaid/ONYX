namespace Onyx.Application.Customers;

/// <summary>
/// Return paths carried through sign-in, sign-up and email links. Only local paths of the public site are accepted
/// (<c>/ar/…</c> or <c>/en/…</c>), so they can never become an open redirect.
/// </summary>
public static class CustomerReturnPath
{
    public const int MaxLength = 500;

    public static string? Clean(string? value)
    {
        var path = value?.Trim();
        if (string.IsNullOrEmpty(path) || path.Length > MaxLength || path.Any(char.IsControl) || path.Contains('\\', StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        return path.StartsWith("/ar/", StringComparison.Ordinal) || path.StartsWith("/en/", StringComparison.Ordinal) ? path : null;
    }
}
