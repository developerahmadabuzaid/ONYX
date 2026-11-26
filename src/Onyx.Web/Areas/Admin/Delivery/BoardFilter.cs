using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Client-side board filters (applied to the loaded board; the URL keeps them shareable).</summary>
public sealed record BoardFilter(string? Assignee, string? Type, string? Priority, string? Label, string? Text)
{
    public bool IsActive => !string.IsNullOrWhiteSpace(Assignee) || !string.IsNullOrWhiteSpace(Type) || !string.IsNullOrWhiteSpace(Priority)
        || !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(Text);

    public bool Matches(IssueListItemResponse issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (!string.IsNullOrWhiteSpace(Assignee)
            && !(Assignee == "none" ? issue.AssigneeId is null : string.Equals(issue.AssigneeId?.ToString(), Assignee, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Type) && !string.Equals(issue.Type, Type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Priority) && !string.Equals(issue.Priority, Priority, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Label) && !issue.Labels.Contains(Label.Trim().ToLowerInvariant(), StringComparer.Ordinal))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(Text)
            || issue.Title.Contains(Text.Trim(), StringComparison.CurrentCultureIgnoreCase)
            || issue.Key.Contains(Text.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
