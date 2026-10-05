using Onyx.Contracts.Leads;

namespace Onyx.Contracts.Booking;

public sealed record BookConsultationRequest(DateTimeOffset StartUtc, InquiryFormDto Form);
