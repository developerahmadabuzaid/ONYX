using Onyx.Contracts.Content;
using Onyx.Web.Localization;

namespace Onyx.Web.Leads;

/// <summary>
/// Where a call to action leads. Conversion actions open the matching form with the page context (service or project) and the
/// call to action itself; URL actions use their target. Tool actions (Phase 2) have no page yet, so they get no link: a call
/// to action is never rendered as a dead link.
/// </summary>
public static class CtaLinks
{
    public static string? Href(LocalizedRouteTable routes, string languageCode, PublicCtaResponse cta, PublicContentResponse? page)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(cta);
        if (cta.Url is not null)
        {
            return cta.Url;
        }

        var pageKey = cta.ActionType switch
        {
            "StartProject" => LeadPages.StartProject,
            "BookConsultation" or "RequestConsultation" => LeadPages.BookConsultation,
            "Contact" => LeadPages.Contact,
            _ => null,
        };
        if (pageKey is null || routes.PathFor(pageKey, languageCode) is not { } path)
        {
            return null;
        }

        var query = new List<string>();
        if (page?.Type is "Service" or "ServicePillar")
        {
            query.Add($"service={page.Id:D}");
        }
        else if (page?.Type is "Project" or "CaseStudy" && pageKey == LeadPages.Contact)
        {
            query.Add($"project={page.Id:D}");
        }

        if (cta.Id is { } ctaId)
        {
            query.Add($"cta={ctaId:D}");
        }

        return query.Count == 0 ? path : path + "?" + string.Join('&', query);
    }
}
