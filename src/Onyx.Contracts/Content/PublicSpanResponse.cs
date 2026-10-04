using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Plain text; <see cref="Href"/> is a site path (unencoded Unicode) or an https URL. Links to offline content are dropped.</summary>
public sealed record PublicSpanResponse(string Text, string? Emphasis, string? Href, bool IsExternal);
