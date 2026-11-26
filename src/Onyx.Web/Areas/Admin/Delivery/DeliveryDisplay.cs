using System.Globalization;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Delivery;
using Onyx.Web.Resources;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Localized labels for delivery values. Default workflow statuses use their system key; custom statuses keep their name.</summary>
public static class DeliveryDisplay
{
    public static readonly string[] IssueTypes = ["Epic", "Story", "Task", "Bug", "Subtask", "Feature", "Improvement", "TechnicalDebt"];
    public static readonly string[] StandardIssueTypes = ["Story", "Task", "Bug", "Feature", "Improvement", "TechnicalDebt"];
    public static readonly string[] Priorities = ["Highest", "High", "Medium", "Low", "Lowest"];
    public static readonly string[] ProjectTypes = ["Software", "SaaS", "AiAgent", "Automation", "Maintenance", "Internal"];
    public static readonly string[] Classifications = ["Client", "Internal"];
    public static readonly string[] ProjectStatuses = ["Planned", "Active", "OnHold", "Completed", "Archived"];
    public static readonly string[] Roles = ["Admin", "Manager", "Developer", "Contributor", "Viewer"];
    public static readonly string[] LinkTypes = ["Blocks", "RelatesTo", "Duplicates"];

    /// <summary>The names the default workflow is created with, so history recorded under those names reads in the UI language.</summary>
    private static readonly Dictionary<string, string> DefaultStatusKeys = new(StringComparer.Ordinal)
    {
        ["Backlog"] = "backlog",
        ["To Do"] = "todo",
        ["In Progress"] = "in-progress",
        ["In Review"] = "in-review",
        ["Testing"] = "testing",
        ["Done"] = "done",
    };

    public static string Status(IStringLocalizer<SharedResource> t, string? systemKey, string name)
    {
        ArgumentNullException.ThrowIfNull(t);
        return string.IsNullOrEmpty(systemKey) ? name : t["Delivery.Status." + systemKey];
    }

    public static string Status(IStringLocalizer<SharedResource> t, WorkflowStatusResponse status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return Status(t, status.SystemKey, status.Name);
    }

    public static string Status(IStringLocalizer<SharedResource> t, IssueListItemResponse issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Status(t, issue.StatusKey, issue.StatusName);
    }

    public static string Value(IStringLocalizer<SharedResource> t, string group, string? value)
    {
        ArgumentNullException.ThrowIfNull(t);
        return string.IsNullOrEmpty(value) ? string.Empty : t[$"Delivery.{group}.{value}"];
    }

    /// <summary>Done share as a whole percent of a count (never an estimate).</summary>
    public static int Percent(int done, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * done / total);

    /// <summary>A value recorded in an issue's history, in the UI language where it is a known value (statuses, enums, minutes).</summary>
    public static string HistoryValue(IStringLocalizer<SharedResource> t, string field, string? value, IReadOnlyCollection<WorkflowStatusResponse> statuses)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(statuses);
        if (string.IsNullOrEmpty(value))
        {
            return "–";
        }

        switch (field)
        {
            case "status":
                var key = statuses.FirstOrDefault(status => status.Name == value)?.SystemKey ?? DefaultStatusKeys.GetValueOrDefault(value);
                return string.IsNullOrEmpty(key) ? value : t["Delivery.Status." + key];
            case "priority" when Priorities.Contains(value):
                return t["Delivery.Priority." + value];
            case "type" when IssueTypes.Contains(value):
                return t["Delivery.Type." + value];
            case "worklog" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes):
                return DeliveryFormat.Duration(t, minutes);
            case "link":
                var space = value.IndexOf(' ', StringComparison.Ordinal);
                var type = space < 0 ? value : value[..space];
                return LinkTypes.Contains(type) ? t[$"Delivery.LinkType.{type}.outward"] + (space < 0 ? string.Empty : value[space..]) : value;
            default:
                return value;
        }
    }
}
