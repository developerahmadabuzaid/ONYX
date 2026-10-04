using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Type"/>: ServicePillar, Service, Industry or Page. <see cref="Presentation"/>: Page (default) or TaxonomyOnly (industries).</summary>
public sealed record CreateContentRequest(string Type, Guid? ParentId, string? Key, string? Presentation, int SortOrder);
