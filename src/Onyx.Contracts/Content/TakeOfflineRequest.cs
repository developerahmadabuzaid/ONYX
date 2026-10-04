using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>
/// For unpublish/archive. <see cref="Resolution"/>: PendingDecision (decide later; URLs answer 410), Gone (no replacement; 410) or
/// RedirectToContent (301 to <see cref="RedirectToContentItemId"/>). Never an automatic homepage redirect.
/// </summary>
public sealed record TakeOfflineRequest(string Resolution, Guid? RedirectToContentItemId, uint Version);
