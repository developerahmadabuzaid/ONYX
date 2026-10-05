namespace Onyx.Domain.Leads;

/// <summary>Where an inquiry came from (architecture.md §13.1). WhatsApp is a future source only.</summary>
public enum InquiryType
{
    StartProject = 0,
    Contact = 1,

    /// <summary>A booked consultation slot.</summary>
    Consultation = 2,

    /// <summary>A consultation request without a booked slot.</summary>
    RequestConsultation = 3,
    ServiceInquiry = 4,
    SimilarProject = 5,

    // Sources of later stages (tools, AI Advisor); not accepted by the public forms yet.
    ToolFollowUp = 6,
    OpportunityFinder = 7,
    ContextualCta = 8,
    Advisor = 9,
}
