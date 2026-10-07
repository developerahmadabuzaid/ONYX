using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>The external Agent's answer: one plain-text headline and summary per requested language.</summary>
public sealed record NewsSummaryDraft(IReadOnlyList<NewsText> Texts);
