using Onyx.Contracts.Media;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record MediaDetailsViewModel(MediaAssetResponse Asset, IReadOnlyList<MediaUsageResponse> Usages);
