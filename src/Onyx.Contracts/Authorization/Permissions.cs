using System.Reflection;

namespace Onyx.Contracts.Authorization;

/// <summary>
/// The single permission catalogue (docs/architecture.md §23). Roles are bundles of these values; code checks permissions, never role names.
/// Values are persisted: append new ones, never rename existing ones.
/// </summary>
public static class Permissions
{
    private static readonly Lazy<IReadOnlyList<string>> AllPermissions = new(Discover);

    /// <summary>Every permission value defined in this catalogue.</summary>
    public static IReadOnlyList<string> All => AllPermissions.Value;

    public static class Pillars
    {
        public const string View = "Pillars.View";
        public const string Create = "Pillars.Create";
        public const string Edit = "Pillars.Edit";
        public const string Delete = "Pillars.Delete";
        public const string Publish = "Pillars.Publish";
    }

    public static class Services
    {
        public const string View = "Services.View";
        public const string Create = "Services.Create";
        public const string Edit = "Services.Edit";
        public const string Delete = "Services.Delete";
        public const string Publish = "Services.Publish";
    }

    public static class Industries
    {
        public const string View = "Industries.View";
        public const string Create = "Industries.Create";
        public const string Edit = "Industries.Edit";
        public const string Delete = "Industries.Delete";
        public const string Publish = "Industries.Publish";
    }

    public static class Projects
    {
        public const string View = "Projects.View";
        public const string Create = "Projects.Create";
        public const string Edit = "Projects.Edit";
        public const string Delete = "Projects.Delete";
        public const string Publish = "Projects.Publish";
    }

    public static class CaseStudies
    {
        public const string View = "CaseStudies.View";
        public const string Create = "CaseStudies.Create";
        public const string Edit = "CaseStudies.Edit";
        public const string Delete = "CaseStudies.Delete";
        public const string Publish = "CaseStudies.Publish";
    }

    public static class Testimonials
    {
        public const string View = "Testimonials.View";
        public const string Create = "Testimonials.Create";
        public const string Edit = "Testimonials.Edit";
        public const string Delete = "Testimonials.Delete";
        public const string Publish = "Testimonials.Publish";
    }

    public static class Proof
    {
        public const string Verify = "Proof.Verify";
    }

    /// <summary>Public showcase of delivered integrations (not system/provider connections).</summary>
    public static class Integrations
    {
        public const string View = "Integrations.View";
        public const string Create = "Integrations.Create";
        public const string Edit = "Integrations.Edit";
        public const string Delete = "Integrations.Delete";
        public const string Publish = "Integrations.Publish";
    }

    public static class Insights
    {
        public const string View = "Insights.View";
        public const string Create = "Insights.Create";
        public const string Edit = "Insights.Edit";
        public const string Delete = "Insights.Delete";
        public const string Publish = "Insights.Publish";
    }

    public static class ContentEngine
    {
        public const string View = "ContentEngine.View";
        public const string Review = "ContentEngine.Review";
        public const string Approve = "ContentEngine.Approve";
    }

    public static class News
    {
        public const string View = "News.View";
        public const string Edit = "News.Edit";
        public const string Publish = "News.Publish";
        public const string Delete = "News.Delete";
    }

    public static class NewsSources
    {
        public const string Manage = "NewsSources.Manage";
    }

    public static class Glossary
    {
        public const string View = "Glossary.View";
        public const string Create = "Glossary.Create";
        public const string Edit = "Glossary.Edit";
        public const string Delete = "Glossary.Delete";
        public const string Publish = "Glossary.Publish";
    }

    public static class Pages
    {
        public const string View = "Pages.View";
        public const string Create = "Pages.Create";
        public const string Edit = "Pages.Edit";
        public const string Delete = "Pages.Delete";
        public const string Publish = "Pages.Publish";
    }

    public static class Ctas
    {
        public const string View = "Ctas.View";
        public const string Create = "Ctas.Create";
        public const string Edit = "Ctas.Edit";
        public const string Delete = "Ctas.Delete";
    }

    public static class Media
    {
        public const string View = "Media.View";
        public const string Upload = "Media.Upload";
        public const string Edit = "Media.Edit";
        public const string Delete = "Media.Delete";
    }

    public static class Seo
    {
        public const string Edit = "Seo.Edit";
    }

    public static class Redirects
    {
        public const string Manage = "Redirects.Manage";
    }

    public static class Offices
    {
        public const string Manage = "Offices.Manage";
    }

    public static class Markets
    {
        public const string View = "Markets.View";
        public const string Create = "Markets.Create";
        public const string Edit = "Markets.Edit";
        public const string Delete = "Markets.Delete";
        public const string Publish = "Markets.Publish";
    }

    public static class Settings
    {
        public const string Manage = "Settings.Manage";
    }

    public static class EmailTemplates
    {
        public const string Manage = "EmailTemplates.Manage";
    }

    public static class Inquiries
    {
        public const string View = "Inquiries.View";
    }

    public static class Leads
    {
        public const string View = "Leads.View";
        public const string ViewAll = "Leads.ViewAll";
        public const string Manage = "Leads.Manage";
        public const string Assign = "Leads.Assign";
        public const string Export = "Leads.Export";
    }

    public static class Bookings
    {
        public const string View = "Bookings.View";
        public const string Manage = "Bookings.Manage";
    }

    public static class Availability
    {
        public const string Manage = "Availability.Manage";
    }

    public static class Analytics
    {
        public const string View = "Analytics.View";
    }

    public static class SearchPerformance
    {
        public const string View = "SearchPerformance.View";
    }

    /// <summary>System/provider connections: GA4, Search Console, LLM, email, calendar, future ad platforms.</summary>
    public static class ProviderConnections
    {
        public const string Manage = "ProviderConnections.Manage";
    }

    public static class Tools
    {
        public const string Configure = "Tools.Configure";
    }

    public static class ToolRuns
    {
        public const string View = "ToolRuns.View";
    }

    public static class Privacy
    {
        public const string Manage = "Privacy.Manage";
    }

    public static class SystemJobs
    {
        public const string View = "System.Jobs.View";
    }

    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    public static class Users
    {
        public const string Manage = "Users.Manage";
    }

    public static class Roles
    {
        public const string Manage = "Roles.Manage";
    }

    private static IReadOnlyList<string> Discover() =>
        typeof(Permissions)
            .GetNestedTypes(BindingFlags.Public)
            .SelectMany(group => group.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList()
            .AsReadOnly();
}
