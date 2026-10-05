namespace Onyx.Domain.Content;

/// <summary>
/// Kinds of CMS content sharing one lifecycle, revision, slug and SEO core. Stored as text; append only.
/// Later stages add their own kinds (e.g. articles, case studies, news) without changing this core.
/// </summary>
public enum ContentType
{
    ServicePillar = 0,
    Service = 1,
    Industry = 2,
    Page = 3,

    // Stage 7: work, proof and knowledge content.
    Project = 4,
    CaseStudy = 5,
    Insight = 6,
    Topic = 7,
    GlossaryTerm = 8,
    Integration = 9,
}
