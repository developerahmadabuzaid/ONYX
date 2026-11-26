using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// A named issue search a user keeps for themself. The criteria are stored as a query string limited to the known filter keys;
/// they are only ever applied as search parameters, never executed or rendered as markup.
/// </summary>
public sealed class SavedIssueFilter : Entity
{
    public const int NameMaxLength = 80;
    public const int QueryMaxLength = 1000;
    public const int ValueMaxLength = 200;
    public const int MaxPerUser = 30;

    /// <summary>The filter keys the issue search understands.</summary>
    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "projectId", "epicId", "assigneeId", "status", "type", "priority", "label", "text", "overdue", "sort",
    };

    private SavedIssueFilter()
    {
        Name = string.Empty;
        Query = string.Empty;
    }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; }

    /// <summary>URL-encoded <c>key=value</c> pairs joined with <c>&amp;</c>, keys from <see cref="AllowedKeys"/>.</summary>
    public string Query { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<SavedIssueFilter> Create(Guid ownerId, string? name, string? query, DateTimeOffset now)
    {
        var cleanName = DeliveryText.Required(name, NameMaxLength, "Delivery.InvalidFilterName", $"Name the filter (at most {NameMaxLength} characters).");
        if (cleanName.IsFailure)
        {
            return cleanName.Error!;
        }

        var cleanQuery = NormalizeQuery(query);
        return cleanQuery.IsFailure
            ? cleanQuery.Error!
            : new SavedIssueFilter { OwnerId = ownerId, Name = cleanName.Value, Query = cleanQuery.Value, CreatedAtUtc = now };
    }

    /// <summary>Keeps the allowed, non-empty pairs in a stable order; rejects anything else.</summary>
    public static Result<string> NormalizeQuery(string? query)
    {
        var invalid = Error.Validation("Delivery.InvalidFilterQuery", "This filter cannot be saved. Change the filters and try again.");
        var text = (query ?? string.Empty).Trim().TrimStart('?');
        if (text.Length is 0 or > QueryMaxLength || text.Any(char.IsControl) || text.Any(char.IsWhiteSpace))
        {
            return invalid;
        }

        var pairs = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                return invalid;
            }

            var key = part[..separator];
            var value = part[(separator + 1)..];
            if (!AllowedKeys.Contains(key) || value.Length > ValueMaxLength || pairs.ContainsKey(key))
            {
                return invalid;
            }

            if (value.Length > 0)
            {
                pairs[key] = value;
            }
        }

        return pairs.Count == 0 ? invalid : string.Join('&', pairs.Select(pair => pair.Key + "=" + pair.Value));
    }
}
