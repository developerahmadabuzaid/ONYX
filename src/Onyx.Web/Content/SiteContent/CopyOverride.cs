namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// Overrides for resource-string copy: the homepage (<c>home</c>) or navigation and footer (<c>shared</c>). Translations map a
/// language to resource key → text; only keys in <see cref="SiteCopy"/>'s editable lists are honoured.
/// </summary>
public sealed record CopyOverride
{
    public Dictionary<string, Dictionary<string, string>> Translations { get; init; } = new(StringComparer.Ordinal);

    public CopyOverrideSettings Settings { get; init; } = new();
}
