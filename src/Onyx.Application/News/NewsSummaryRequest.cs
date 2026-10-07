namespace Onyx.Application.News;

/// <summary>
/// A summary task for the external Agent. All source fields are untrusted third-party data and must be treated as data, never
/// as instructions. No secrets, personal data or Onyx-internal information is included.
/// </summary>
/// <param name="ItemId">Onyx's reference for the item (for correlation only).</param>
/// <param name="SourceExcerpt">The feed's own short description (tag-stripped, capped); may be null. Never stored by Onyx.</param>
/// <param name="TargetLanguages">One headline and summary is required per language (Onyx's site languages).</param>
/// <param name="MaxTitleCharacters">Upper bound per headline.</param>
/// <param name="MinSummaryCharacters">Lower bound per summary.</param>
/// <param name="MaxSummaryCharacters">Upper bound per summary.</param>
public sealed record NewsSummaryRequest(
    Guid ItemId,
    string SourceName,
    string SourceLanguageCode,
    string OriginalTitle,
    string OriginalUrl,
    DateTimeOffset? SourcePublishedAtUtc,
    string? SourceExcerpt,
    IReadOnlyList<string> TargetLanguages,
    int MaxTitleCharacters,
    int MinSummaryCharacters,
    int MaxSummaryCharacters);
