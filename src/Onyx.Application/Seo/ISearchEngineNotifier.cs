namespace Onyx.Application.Seo;

/// <summary>
/// Tells participating search engines (IndexNow: Bing and others; not Google) that public URLs changed. Inactive until a key is
/// configured and production indexing is enabled; while inactive every call is a no-op.
/// </summary>
public interface ISearchEngineNotifier
{
    bool IsActive { get; }

    /// <summary>The public verification key served at the key location, or null while inactive (the key file then answers 404).</summary>
    string? VerificationKey { get; }

    /// <param name="sitePaths">Site paths with the language prefix, unencoded.</param>
    /// <exception cref="SearchEngineNotificationDeferredException">The engine asked to retry later (rate limit or server error).</exception>
    Task NotifyAsync(IReadOnlyCollection<string> sitePaths, CancellationToken cancellationToken);
}
