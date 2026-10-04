using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary><see cref="Source"/>: KeywordMap or SeoProfile.</summary>
public sealed record KeywordConflictItem(string Source, Guid? ContentItemId, string Keyword, string LanguageCode, string? SearchIntent, string? TargetMarket);
