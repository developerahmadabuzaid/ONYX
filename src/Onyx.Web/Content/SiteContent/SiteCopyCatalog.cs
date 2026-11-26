namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// The resource strings editors may override, grouped as the admin editors show them. Anything not listed here (form labels,
/// validation messages, legal and system text) stays code-owned. Keys keep their resource-file value as the fallback.
/// </summary>
public static class SiteCopyCatalog
{
    /// <summary>Homepage sections that can be hidden or reordered (the hero and services always render).</summary>
    public static IReadOnlyList<string> HomeMovableSections { get; } = ["process", "why", "stack", "security"];

    public static IReadOnlyList<string> HomeHideableSections { get; } = ["process", "why", "stack", "security", "cta"];

    public static IReadOnlyList<CopyGroup> Home { get; } =
    [
        new("seo", ["Home.Seo.Title", "Home.Seo.OgTitle", "Home.Seo.Description"]),
        new("hero", ["Home.Hero.Eyebrow", "Home.Hero.Line1", "Home.Hero.Line2", "Home.Hero.Lead", "Home.Hero.Explore", "Home.Hero.PointsLabel", "Home.Hero.Point1", "Home.Hero.Point2", "Home.Hero.Point3"]),
        new("services",
        [
            "Home.Services.Eyebrow", "Home.Services.Title", "Home.Services.Lead",
            "Home.Service.Ai.Title", "Home.Service.Ai.Text", "Home.Service.Software.Title", "Home.Service.Software.Text",
            "Home.Service.Cloud.Title", "Home.Service.Cloud.Text", "Home.Service.Security.Title", "Home.Service.Security.Text", "Home.Service.More",
        ]),
        new("process",
        [
            "Home.Process.Eyebrow", "Home.Process.Title", "Home.Process.Lead",
            "Home.Process.Step1.Title", "Home.Process.Step1.Text", "Home.Process.Step2.Title", "Home.Process.Step2.Text",
            "Home.Process.Step3.Title", "Home.Process.Step3.Text", "Home.Process.Step4.Title", "Home.Process.Step4.Text",
            "Home.Process.Step5.Title", "Home.Process.Step5.Text", "Home.Process.More",
        ]),
        new("why",
        [
            "Home.Why.Eyebrow", "Home.Why.Title", "Home.Why.Lead",
            "Home.Why.Partnership.Title", "Home.Why.Partnership.Text", "Home.Why.Expertise.Title", "Home.Why.Expertise.Text",
            "Home.Why.Outcomes.Title", "Home.Why.Outcomes.Text", "Home.Why.Scalable.Title", "Home.Why.Scalable.Text", "Home.Why.More",
        ]),
        new("stack",
        [
            "Home.Stack.Eyebrow", "Home.Stack.Title", "Home.Stack.Lead", "Home.Stack.Note",
            "Home.Stack.Cloud.Title", "Home.Stack.Cloud.Items", "Home.Stack.Software.Title", "Home.Stack.Software.Items",
            "Home.Stack.Data.Title", "Home.Stack.Data.Items", "Home.Stack.Ai.Title", "Home.Stack.Ai.Items", "Home.Stack.More",
        ]),
        new("security",
        [
            "Home.Security.Eyebrow", "Home.Security.Title", "Home.Security.Lead", "Home.Security.Note",
            "Home.Security.Architecture.Title", "Home.Security.Architecture.Text", "Home.Security.Access.Title", "Home.Security.Access.Text",
            "Home.Security.Data.Title", "Home.Security.Data.Text", "Home.Security.Monitoring.Title", "Home.Security.Monitoring.Text",
            "Home.Security.Backup.Title", "Home.Security.Backup.Text", "Home.Security.Deployment.Title", "Home.Security.Deployment.Text", "Home.Security.More",
        ]),
        new("insights", ["Home.Insights.Eyebrow", "Home.Insights.Title", "Home.Insights.Lead", "Home.Insights.All", "Home.FeaturedWork", "Home.Work.Title", "Home.Work.All", "Home.LatestNews"]),
        new("cta", ["Home.Cta.Title", "Home.Cta.Text", "Home.Cta.Services"]),
    ];

    public static IReadOnlyList<CopyGroup> Shared { get; } =
    [
        new("navigation", ["Public.Nav.Home", "Public.Nav.Services", "Public.Nav.About", "Public.Nav.Insights", "Public.Nav.Contact", "Public.Nav.Work", "Public.Nav.News", "Public.Menu"]),
        new("mega", ["Public.Mega.Statement", "Public.Mega.All", "Public.Mega.More"]),
        new("ctas", ["Public.Cta.Talk", "Public.Cta.StartProject", "Public.Cta.BookConsultation"]),
        new("footer", ["Public.Footer.Statement", "Public.Footer.Services", "Public.Footer.Company", "Public.Footer.Contact", "Public.Footer.Legal", "Public.Footer.Offices"]),
    ];

    /// <summary>Keys whose value is a list separated by "|" (technology tags).</summary>
    public static bool IsList(string key) => key.EndsWith(".Items", StringComparison.Ordinal);

    public static bool IsHomeKey(string key) => HomeKeys.Contains(key);

    public static bool IsSharedKey(string key) => SharedKeys.Contains(key);

    private static readonly HashSet<string> HomeKeys = new(Home.SelectMany(group => group.Keys), StringComparer.Ordinal);

    private static readonly HashSet<string> SharedKeys = new(Shared.SelectMany(group => group.Keys), StringComparer.Ordinal);
}
