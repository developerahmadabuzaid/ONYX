using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Paths are without the language prefix. Targets are internal only (a content item or a site path).</summary>
public sealed record SaveRedirectRequest(
    string LanguageCode, string FromPath, string Kind, Guid? TargetContentItemId, string? TargetPath, string? Note, bool IsActive);
