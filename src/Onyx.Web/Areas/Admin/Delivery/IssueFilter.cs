using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>Issue search filters bound from the query string.</summary>
public sealed class IssueFilter
{
    public Guid? ProjectId { get; set; }

    public Guid? EpicId { get; set; }

    public Guid? AssigneeId { get; set; }

    /// <summary>Open, ToDo, InProgress or Done.</summary>
    public string? Status { get; set; }

    public string? Type { get; set; }

    public string? Priority { get; set; }

    public string? Label { get; set; }

    public string? Text { get; set; }

    public bool Overdue { get; set; }

    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    /// <summary>The non-empty filters as a query string (the form a saved filter keeps).</summary>
    public string ToQuery()
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(key + "=" + Uri.EscapeDataString(value.Trim()));
            }
        }

        Add("projectId", ProjectId?.ToString("D"));
        Add("epicId", EpicId?.ToString("D"));
        Add("assigneeId", AssigneeId?.ToString("D"));
        Add("status", Status);
        Add("type", Type);
        Add("priority", Priority);
        Add("label", Label);
        Add("text", Text);
        Add("overdue", Overdue ? "true" : null);
        Add("sort", Sort);
        return string.Join('&', parts);
    }
}
