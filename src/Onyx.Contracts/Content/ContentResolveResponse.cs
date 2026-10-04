using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>
/// <see cref="Kind"/>: Content (200), Redirect (301 to <see cref="RedirectPath"/>, a full site path), Gone (410) or NotFound (404).
/// </summary>
public sealed record ContentResolveResponse(string Kind, string? RedirectPath, PublicContentResponse? Content);
