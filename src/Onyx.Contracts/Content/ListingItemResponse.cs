using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

/// <summary>A live item in a public section listing. <see cref="Path"/> is the full site path (unencoded).</summary>
public sealed record ListingItemResponse(Guid Id, string Type, string Title, string? Summary, string Path, DateTimeOffset? PublishedAtUtc, PublicMediaResponse? FeaturedImage);
