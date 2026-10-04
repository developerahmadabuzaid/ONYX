using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record PublicContentResponse(
    Guid Id,
    string Type,
    string LanguageCode,
    string Title,
    string Summary,
    IReadOnlyList<PublicBlockResponse> Body,
    IReadOnlyList<FaqDto> Faqs,
    PublicSeoResponse Seo,
    IReadOnlyList<BreadcrumbResponse> Breadcrumbs,
    IReadOnlyList<RelatedContentResponse> Related,
    PublicCtaResponse? Cta,
    PublicMediaResponse? FeaturedImage,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? ModifiedAtUtc);
