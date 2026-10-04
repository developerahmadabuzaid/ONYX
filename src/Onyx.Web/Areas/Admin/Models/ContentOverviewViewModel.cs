using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentOverviewViewModel(
    ContentDetailResponse Content, QualityReportResponse? Quality, PublishingTimeRecommendationResponse? PublishingTime, PreviewLinkResponse? PreviewLink);
