using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Booking;

/// <summary>A date (in the business time zone) with no consultation slots, such as a public holiday.</summary>
public sealed class AvailabilityException : Entity, IAuditable
{
    public const int ReasonMaxLength = 200;

    private AvailabilityException()
    {
    }

    public DateOnly Date { get; private set; }

    public string? Reason { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Leads;

    public static Result<AvailabilityException> Create(DateOnly date, string? reason)
    {
        var clean = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (clean is { Length: > ReasonMaxLength } || (clean is not null && ContentBodyRules.HasControlCharacters(clean, allowNewLines: false)))
        {
            return Error.Validation("Availability.InvalidReason", $"Reasons are limited to {ReasonMaxLength} characters.");
        }

        return new AvailabilityException { Date = date, Reason = clean };
    }
}
