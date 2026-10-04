namespace Onyx.Domain.Content;

/// <summary>Conversion actions (CLAUDE.md §2.4). Stored as text; append only. WhatsApp is reserved and cannot be used at launch.</summary>
public enum CtaActionType
{
    StartProject = 0,
    BookConsultation = 1,
    Contact = 2,
    RequestConsultation = 3,
    AnalyzeWebsite = 4,
    CheckSecurityPosture = 5,
    DiscoverOpportunities = 6,
    Url = 7,
    WhatsApp = 8,
}
