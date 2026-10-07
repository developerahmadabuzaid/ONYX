using Onyx.Domain.Common;
using Onyx.Domain.News;

namespace Onyx.Application.News;

/// <summary>
/// Reads an allowlisted feed through the SSRF-safe outbound gateway and returns its entries (at most <c>maxEntries</c>).
/// Failures are expected results with a safe code (see <see cref="NewsFeedErrors"/>); nothing of the response body is
/// returned or logged on failure.
/// </summary>
public interface INewsFeedReader
{
    Task<Result<IReadOnlyList<FeedEntry>>> ReadAsync(Uri feedUrl, NewsFeedFormat expectedFormat, int maxEntries, CancellationToken cancellationToken);
}
