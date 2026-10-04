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
}
