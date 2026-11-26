namespace Onyx.Application.Delivery;

/// <summary>
/// Ordering of issues with sparse ranks. A moved issue takes the midpoint between its new neighbours; when two neighbours are
/// adjacent, the caller renumbers the project and asks again. Lists (board columns, sprints, backlog) all sort by the same rank,
/// so a position saved in one view stays consistent in the others.
/// </summary>
public static class IssueRanking
{
    public const long Spacing = 1024;

    /// <summary>
    /// The rank that places an issue before <paramref name="beforeIssueId"/> (or last when null) in <paramref name="list"/>, which
    /// must not contain the moved issue. Null when there is no room between the neighbours; NotInList when the anchor is unknown.
    /// </summary>
    public static (long? Rank, bool NotInList) Place(IReadOnlyList<IssueRankSlot> list, Guid? beforeIssueId)
    {
        ArgumentNullException.ThrowIfNull(list);
        var ordered = list.OrderBy(slot => slot.Rank).ToList();
        if (beforeIssueId is not { } anchor)
        {
            return (ordered.Count == 0 ? Spacing : ordered[^1].Rank + Spacing, false);
        }

        var index = ordered.FindIndex(slot => slot.IssueId == anchor);
        if (index < 0)
        {
            return (null, true);
        }

        var next = ordered[index].Rank;
        if (index == 0)
        {
            return (next - Spacing, false);
        }

        var previous = ordered[index - 1].Rank;
        return next - previous >= 2 ? (previous + ((next - previous) / 2), false) : (null, false);
    }
}
