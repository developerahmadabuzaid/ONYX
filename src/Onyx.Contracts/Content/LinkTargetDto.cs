using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Exactly one of: a content item id, a site path, or an absolute https URL.</summary>
public sealed record LinkTargetDto(Guid? ContentItemId = null, string? Path = null, string? Url = null);
