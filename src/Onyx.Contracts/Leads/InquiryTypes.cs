namespace Onyx.Contracts.Leads;

/// <summary>Inquiry type names on the API. Bookings use the consultations resource, not the inquiries form endpoint.</summary>
public static class InquiryTypes
{
    public const string StartProject = "StartProject";
    public const string Contact = "Contact";
    public const string Consultation = "Consultation";
    public const string RequestConsultation = "RequestConsultation";
    public const string ServiceInquiry = "ServiceInquiry";
    public const string SimilarProject = "SimilarProject";
    public const string ContextualCta = "ContextualCta";

    /// <summary>Types accepted by <c>POST /api/v1/inquiries</c> at launch (tools and the advisor arrive in later phases).</summary>
    public static IReadOnlySet<string> PublicForms { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        StartProject, Contact, RequestConsultation, ServiceInquiry, SimilarProject, ContextualCta,
    };
}
