namespace Onyx.Contracts.SiteContent;

/// <summary>Keys of the editable site content documents (see CLAUDE.md §9 "Site pages").</summary>
public static class SiteContentKeys
{
    public const string Home = "home";
    public const string Shared = "shared";
    public const string Settings = "settings";
    public const string PagePrefix = "page:";

    public static string Page(string pageKey) => PagePrefix + pageKey;
}
