using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>A live, indexable, self-canonical page. <see cref="Path"/> and <see cref="Alternates"/> are full site paths (unencoded).</summary>
public sealed record SitemapEntryResponse(string Path, DateTimeOffset? LastModifiedUtc, IReadOnlyDictionary<string, string> Alternates);
