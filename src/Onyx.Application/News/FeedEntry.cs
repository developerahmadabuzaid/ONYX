namespace Onyx.Application.News;

/// <summary>
/// One feed entry as read. <see cref="Excerpt"/> is the source's own description, tag-stripped and length-capped; it is
/// processed transiently (classification, summary checks, the external summary service) and never stored.
/// </summary>
/// <param name="Link">The entry's article link (null when absent or not an absolute URL).</param>
/// <param name="PublishedAtUtc">The entry's publication time when the feed gives one (published, else updated); never invented.</param>
public sealed record FeedEntry(Uri? Link, string Title, DateTimeOffset? PublishedAtUtc, IReadOnlyList<string> Categories, string? Excerpt);
