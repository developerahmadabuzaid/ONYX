using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>
/// Paths include the language prefix and are unencoded Unicode; the site percent-encodes them. <see cref="Alternates"/> lists only
/// languages in which the item is live. <see cref="StructuredData"/> lists only types the visible content supports.
/// </summary>
public sealed record PublicSeoResponse(
    string Title,
    string? Description,
    string CanonicalPath,
    bool Index,
    bool Follow,
    IReadOnlyDictionary<string, string> Alternates,
    string? XDefaultPath,
    string? OpenGraphTitle,
    string? OpenGraphDescription,
    PublicMediaResponse? OpenGraphImage,
    IReadOnlyList<string> StructuredData);
