using Onyx.Application.News;
using Onyx.Application.Providers;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.News;

/// <summary>
/// The registered <see cref="INewsSummaryService"/> until Onyx's external AI Agent is integrated: it never produces text, so
/// every ingested item stays Held for editors and nothing is published automatically. Connecting the Agent means registering
/// an adapter for this interface; the News Hub model and pages do not change.
/// </summary>
internal sealed class NotConfiguredNewsSummaryService : INewsSummaryService
{
    public string ProviderKey => ProviderKeys.NewsSummaries;

    public Task<Result<NewsSummaryDraft>> SummarizeAsync(NewsSummaryRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<NewsSummaryDraft>>(Error.Unavailable(NewsSummaryErrors.NotConfigured, "No news summary service is connected."));
}
