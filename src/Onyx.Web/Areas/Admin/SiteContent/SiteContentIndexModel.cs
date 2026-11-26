using Onyx.Contracts.SiteContent;
using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Areas.Admin.SiteContent;

/// <summary>Every editable part of the public site with its editing status.</summary>
public sealed record SiteContentIndexModel(
    IReadOnlyList<SitePageDefinition> Pages,
    IReadOnlyDictionary<string, SiteContentDocumentSummaryResponse> Documents,
    string LanguageCode)
{
    public SiteContentDocumentSummaryResponse? Status(string key) => Documents.GetValueOrDefault(key);
}
