namespace Onyx.Application.Localization;

/// <summary>
/// Languages accepted for admin preferences and email templates. Moves to strongly typed localization configuration in
/// Stage 4; business logic must still never branch on a specific language.
/// </summary>
public static class SupportedLanguages
{
    public const string Default = "ar";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { "ar", "en" };
}
