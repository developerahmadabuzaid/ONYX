using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Web.Content;

/// <summary>A public section listing (work, case studies, insights, topics, glossary, integrations).</summary>
public sealed record SectionListingViewModel(string Section, string LanguageCode, PagedResult<ListingItemResponse> Items);
