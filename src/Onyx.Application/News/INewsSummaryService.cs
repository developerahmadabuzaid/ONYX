using Onyx.Domain.Common;

namespace Onyx.Application.News;

/// <summary>
/// The integration boundary to Onyx's externally built AI Agent for news summaries (architecture.md §17.1). Onyx sends a
/// structured task: the source facts and the limits the result must meet. How the summary is written (prompts, model,
/// reasoning, tools) belongs entirely to the external Agent; nothing of that lives in this codebase. Onyx then checks every
/// result itself (languages, lengths, plain text, no verbatim copying) before it is stored, and only an allowlisted source
/// set to auto-publish ever publishes without review.
/// <para>
/// Until an adapter for the Agent is registered and configured, the registered implementation answers
/// <see cref="NewsSummaryErrors.NotConfigured"/> and every item stays Held for editors. Failures and timeouts are expected
/// results: the item is held, never published.
/// </para>
/// </summary>
public interface INewsSummaryService
{
    /// <summary>Stable provider key reported in <c>provider_connections</c> and stored on generated items.</summary>
    string ProviderKey { get; }

    Task<Result<NewsSummaryDraft>> SummarizeAsync(NewsSummaryRequest request, CancellationToken cancellationToken);
}
