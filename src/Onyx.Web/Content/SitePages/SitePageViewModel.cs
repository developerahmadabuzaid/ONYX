namespace Onyx.Web.Content.SitePages;

/// <summary>What <c>SitePage/Show</c> renders: the page in the request language, its breadcrumb trail and its link targets.</summary>
public sealed record SitePageViewModel(
    SitePageDefinition Page,
    SitePageText Text,
    string LanguageCode,
    IReadOnlyList<(string Title, string Path)> Breadcrumbs,
    IReadOnlyList<(SitePageDefinition Page, SitePageText Text, string Path)> Related);
