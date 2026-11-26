using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// The project health rule shown on dashboards, stated rather than estimated: Completed and OnHold come from the project status;
/// an open project past its target date is Delayed; one with overdue open issues is AtRisk; otherwise it is OnTrack.
/// </summary>
public static class ProjectHealth
{
    public const string OnTrack = "OnTrack";
    public const string AtRisk = "AtRisk";
    public const string Delayed = "Delayed";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";

    public static string Of(DeliveryProjectStatus status, DateOnly? targetDate, int overdueOpenIssues, DateOnly today) => status switch
    {
        DeliveryProjectStatus.Completed => Completed,
        DeliveryProjectStatus.OnHold => OnHold,
        _ when targetDate is { } target && target < today => Delayed,
        _ when overdueOpenIssues > 0 => AtRisk,
        _ => OnTrack,
    };
}
