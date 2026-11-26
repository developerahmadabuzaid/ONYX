namespace Onyx.Contracts.SiteContent;

/// <summary>A social profile. <see cref="Network"/> is one of <see cref="Networks"/>; <see cref="Url"/> must be https.</summary>
public sealed record SiteSocialLink
{
    public static IReadOnlyList<string> Networks { get; } = ["linkedin", "x", "instagram", "facebook", "youtube", "tiktok", "snapchat", "github"];

    public string Network { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;
}
