using Onyx.Domain.Booking;
using Onyx.Domain.Leads;

namespace Onyx.Application.Privacy;

/// <summary>
/// Everything stored about one data subject outside their admin account: leads and inquiries with that normalized email, their
/// consent records, bookings, notes and activities. Loaded tracked, so erasure can change them through their domain methods.
/// </summary>
public sealed record SubjectRecords(
    IReadOnlyList<Lead> Leads,
    IReadOnlyList<Inquiry> Inquiries,
    IReadOnlyList<ConsentRecord> Consents,
    IReadOnlyList<ConsultationBooking> Bookings,
    IReadOnlyList<LeadNote> Notes,
    IReadOnlyList<LeadActivity> Activities)
{
    public static SubjectRecords Empty { get; } = new([], [], [], [], [], []);
}
