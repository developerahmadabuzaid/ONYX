using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Kind"/>: Permanent (301) or Gone (410).</summary>
public sealed record RedirectResponse(
    Guid Id, string LanguageCode, string FromPath, string Kind, Guid? TargetContentItemId, string? TargetPath, string? Note, bool IsActive, DateTimeOffset CreatedAtUtc);
