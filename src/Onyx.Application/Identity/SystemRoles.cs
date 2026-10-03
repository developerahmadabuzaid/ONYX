using Onyx.Contracts.Authorization;

namespace Onyx.Application.Identity;

/// <summary>
/// Seeded roles (docs/architecture.md §23). Only <see cref="SuperAdmin"/> is fixed: it always holds every permission and
/// cannot be edited or deleted. The other roles are default bundles that administrators may change later.
/// </summary>
public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ContentManager = "ContentManager";
    public const string Marketing = "Marketing";
    public const string Sales = "Sales";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultBundles { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [ContentManager] = Group(
            "Pillars", "Services", "Industries", "Projects", "CaseStudies", "Testimonials", "Integrations", "Insights",
            "ContentEngine", "News", "NewsSources", "Glossary", "Pages", "Ctas", "Media", "Seo", "Redirects", "Offices", "EmailTemplates")
            .Append(Permissions.Markets.View)
            .ToList(),
        [Marketing] =
        [
            Permissions.Analytics.View,
            Permissions.SearchPerformance.View,
            Permissions.Seo.Edit,
            Permissions.Ctas.View,
            Permissions.Ctas.Create,
            Permissions.Ctas.Edit,
            Permissions.ContentEngine.View,
            Permissions.ContentEngine.Review,
            Permissions.Insights.View,
            Permissions.Insights.Create,
            Permissions.Insights.Edit,
            Permissions.Pages.View,
            Permissions.Services.View,
            Permissions.Industries.View,
            Permissions.Projects.View,
            Permissions.News.View,
        ],
        [Sales] =
        [
            Permissions.Inquiries.View,
            Permissions.Leads.View,
            Permissions.Leads.Manage,
            Permissions.Bookings.View,
            Permissions.Bookings.Manage,
            Permissions.Availability.Manage,
        ],
    };

    private static IEnumerable<string> Group(params string[] prefixes) =>
        Permissions.All.Where(permission => prefixes.Any(prefix => permission.StartsWith(prefix + ".", StringComparison.Ordinal)));
}
