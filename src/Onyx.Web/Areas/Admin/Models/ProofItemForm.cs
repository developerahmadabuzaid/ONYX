using Onyx.Contracts.Common;
using Onyx.Contracts.Proof;

namespace Onyx.Web.Areas.Admin.Models;

/// <summary>One proof item as edited in the admin: name/label/quote and description per language, plus kind-specific evidence.</summary>
public sealed class ProofItemForm
{
    public string Kind { get; set; } = "Capability";

    public string? TitleAr { get; set; }

    public string? DescriptionAr { get; set; }

    public string? TitleEn { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Value { get; set; }

    public DateOnly? AsOf { get; set; }

    public string? AttributionName { get; set; }

    public string? AttributionRole { get; set; }

    public string? AttributionOrganization { get; set; }

    public int SortOrder { get; set; }

    public SaveProofItemRequest ToRequest(Guid contentItemId, uint? version)
    {
        var texts = new List<LocalizedTextDto>();
        if (!string.IsNullOrWhiteSpace(TitleAr))
        {
            texts.Add(new LocalizedTextDto("ar", TitleAr.Trim(), string.IsNullOrWhiteSpace(DescriptionAr) ? null : DescriptionAr.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(TitleEn))
        {
            texts.Add(new LocalizedTextDto("en", TitleEn.Trim(), string.IsNullOrWhiteSpace(DescriptionEn) ? null : DescriptionEn.Trim()));
        }

        return new SaveProofItemRequest(contentItemId, Kind, texts, Value, AsOf, AttributionName, AttributionRole, AttributionOrganization, SortOrder, version);
    }
}
