using Microsoft.Extensions.Options;
using Onyx.Web.ApiClients;

namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// Serves the published site content overrides from memory. A stale snapshot keeps being served while one background refresh
/// runs, so page views never wait on the API (except the first request after start-up, for at most a short, configured time).
/// When the API fails the last good snapshot stays in use; with none, pages render the code-owned copy.
/// </summary>
public sealed partial class SiteContentProvider(IServiceProvider services, IOptions<SiteContentOptions> options, TimeProvider timeProvider, ILogger<SiteContentProvider> logger)
{
    private readonly Lock _gate = new();
    private (SiteContentSnapshot Snapshot, DateTimeOffset LoadedAt)? _state;
    private Task? _refresh;

    public SiteContentSnapshot Current => _state?.Snapshot ?? SiteContentSnapshot.Empty;

    public async Task EnsureFreshAsync(CancellationToken cancellationToken)
    {
        var state = _state;
        if (state is { } loaded && timeProvider.GetUtcNow() - loaded.LoadedAt < TimeSpan.FromSeconds(options.Value.CacheSeconds))
        {
            return;
        }

        var refresh = StartRefresh();
        if (state is null)
        {
            await Task.WhenAny(refresh, Task.Delay(options.Value.InitialWaitMilliseconds, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// After a publish from this instance: reloads now so the editor's next page view already shows the new version. A refresh
    /// already in flight may have read the old version, so it is awaited first. The wait is bounded; on timeout the reload
    /// still completes in the background.
    /// </summary>
    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        var wait = TimeSpan.FromMilliseconds(options.Value.InitialWaitMilliseconds);
        Task? running;
        lock (_gate)
        {
            running = _refresh is { IsCompleted: false } inFlight ? inFlight : null;
        }

        if (running is not null)
        {
            await Task.WhenAny(running, Task.Delay(wait, cancellationToken)).ConfigureAwait(false);
        }

        Invalidate();
        await Task.WhenAny(StartRefresh(), Task.Delay(wait, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Marks the cached copy stale: the next request starts a reload (and keeps serving the cached copy meanwhile).</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            if (_state is { } state)
            {
                _state = (state.Snapshot, DateTimeOffset.MinValue);
            }
        }
    }

    private Task StartRefresh()
    {
        lock (_gate)
        {
            if (_refresh is { IsCompleted: false } running)
            {
                return running;
            }

            _refresh = Task.Run(RefreshAsync);
            return _refresh;
        }
    }

    private async Task RefreshAsync()
    {
        SiteContentSnapshot? loaded = null;
        try
        {
            var client = services.GetRequiredService<IPublicSiteContentApiClient>();
            var result = await client.GetPublishedAsync(CancellationToken.None).ConfigureAwait(false);
            if (result.IsSuccess && result.Value is not null)
            {
                loaded = SiteContentSnapshot.From(result.Value.Documents);
            }
            else
            {
                LogLoadFailed(logger, (int)result.StatusCode);
            }
        }
#pragma warning disable CA1031 // A failed refresh must never take the site down; the previous snapshot stays in use.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogLoadError(logger, exception);
        }

        lock (_gate)
        {
            // On failure keep what we had (or the empty fallback) and retry after the normal cache period.
            _state = (loaded ?? _state?.Snapshot ?? SiteContentSnapshot.Empty, timeProvider.GetUtcNow());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Site content could not be loaded from the API (status {Status}); serving the previous or code-owned content.")]
    private static partial void LogLoadFailed(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Site content refresh failed; serving the previous or code-owned content.")]
    private static partial void LogLoadError(ILogger logger, Exception exception);
}
