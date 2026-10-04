using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record UpdateContentSettingsRequest(
    Guid? ParentId, string? Key, string Presentation, int SortOrder, Guid? PrimaryCtaId, Guid? FeaturedImageId, uint Version);
