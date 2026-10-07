using System.Diagnostics;
using System.Net;
using System.Net.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.News;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Outbound;

/// <summary>
/// The only way Onyx fetches a visitor- or admin-supplied external URL (CLAUDE.md §8, architecture.md §25). Rules:
/// <list type="bullet">
/// <item>http(s) only (https only when required), no credentials in the URL, default ports only, a public DNS name (no IP
/// literals, numeric hosts or internal names);</item>
/// <item>the host is resolved once per connection and every resolved address must be public
/// (<see cref="OutboundAddressPolicy"/>); the connection is then made to that vetted address, so a DNS answer that changes
/// between check and connect (rebinding) cannot reach an internal address;</item>
/// <item>no proxy, no cookies, no credentials; certificates are validated normally;</item>
/// <item>redirects are followed manually, each target re-validated, at most <see cref="SafeOutboundHttpOptions.MaxRedirects"/>,
/// and never from https to http when https is required;</item>
/// <item>one timeout for the whole request and a cap on the (decompressed) body;</item>
/// <item>every request is logged with host, status, size and duration only, never the path, query or body.</item>
/// </list>
/// </summary>
public sealed partial class SafeOutboundHttpClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly SafeOutboundHttpOptions _options;
    private readonly ILogger<SafeOutboundHttpClient> _logger;

    public SafeOutboundHttpClient(
        IOptions<SafeOutboundHttpOptions> options, IOutboundDnsResolver resolver, IOutboundConnector connector, ILogger<SafeOutboundHttpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(connector);
        _options = options.Value;
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            Credentials = null,
            PreAuthenticate = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds),
            MaxResponseHeadersLength = 64,
            MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions = new SslClientAuthenticationOptions(),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var port = context.DnsEndPoint.Port;
                if (port is not (80 or 443))
                {
                    throw new SafeOutboundException(NewsFeedErrors.Blocked, "Only the standard ports are allowed.");
                }

                IPAddress[] addresses;
                try
                {
                    addresses = await resolver.ResolveAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new SafeOutboundException(NewsFeedErrors.Unreachable, "The host could not be resolved.", exception);
                }

                // Any non-public answer refuses the whole host: a mixed answer is how rebinding attacks start.
                if (addresses.Length == 0 || !addresses.All(OutboundAddressPolicy.IsPublic))
                {
                    throw new SafeOutboundException(NewsFeedErrors.Blocked, "The host does not resolve to public addresses only.");
                }

                return await connector.ConnectAsync(addresses[0], port, cancellationToken).ConfigureAwait(false);
            },
        };

        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
    }

    /// <summary>Product token of the User-Agent (what robots.txt groups are matched against).</summary>
    public string UserAgentProduct => _options.UserAgent.Split('/', 2)[0];

    /// <summary>Fetches <paramref name="url"/> with every rule applied; failures throw <see cref="SafeOutboundException"/> with a safe code.</summary>
    public async Task<SafeOutboundResponse> GetAsync(Uri url, SafeOutboundRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(request);
        var maxBytes = Math.Min(request.MaxResponseBytes ?? _options.MaxResponseBytes, _options.MaxResponseBytes);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var started = Stopwatch.GetTimestamp();
        var current = Validate(url, request.RequireHttps);
        try
        {
            for (var redirects = 0; ; redirects++)
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, current);
                message.Headers.Accept.ParseAdd(request.Accept);
                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                LogRequest(_logger, current.IdnHost, status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

                if (status is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    if (redirects >= _options.MaxRedirects)
                    {
                        throw new SafeOutboundException(NewsFeedErrors.TooManyRedirects, "Too many redirects.");
                    }

                    var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                    // Never downgrade: once on https, every further hop must be https as well.
                    current = Validate(next, request.RequireHttps || current.Scheme == Uri.UriSchemeHttps);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new SafeOutboundException(NewsFeedErrors.HttpStatus, $"The server answered {status}.") { StatusCode = status };
                }

                if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
                {
                    throw new SafeOutboundException(NewsFeedErrors.TooLarge, "The response is too large.");
                }

                var body = await ReadCappedAsync(response.Content, maxBytes, timeout.Token).ConfigureAwait(false);
                return new SafeOutboundResponse(status, response.Content.Headers.ContentType?.MediaType, current, body);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SafeOutboundException(NewsFeedErrors.Timeout, "The request timed out.");
        }
        catch (HttpRequestException exception)
        {
            // A refusal raised while connecting surfaces wrapped; keep its code. Never pass the transport message on.
            var refusal = FindRefusal(exception);
            throw refusal is not null
                ? new SafeOutboundException(refusal.Code, refusal.Message, exception)
                : new SafeOutboundException(NewsFeedErrors.Unreachable, "The server could not be reached.", exception);
        }
    }

    public void Dispose() => _client.Dispose();

    /// <summary>The URL rules checked before every hop (the network-level checks follow when connecting).</summary>
    public static Uri Validate(Uri url, bool requireHttps)
    {
        ArgumentNullException.ThrowIfNull(url);
        var checkedUrl = NewsUrl.Check(url, requireHttps);
        if (checkedUrl.IsFailure)
        {
            throw new SafeOutboundException(NewsFeedErrors.Blocked, checkedUrl.Error!.Message);
        }

        // Fragments are never sent; drop them so logs and redirects stay clean.
        return new UriBuilder(checkedUrl.Value) { Fragment = string.Empty }.Uri;
    }

    private static async Task<byte[]> ReadCappedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1_024];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                // Counted after decompression, so a compressed "bomb" stops at the cap too.
                throw new SafeOutboundException(NewsFeedErrors.TooLarge, "The response is too large.");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static SafeOutboundException? FindRefusal(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SafeOutboundException refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbound GET to {Host} answered {StatusCode} after {ElapsedMs:0} ms")]
    private static partial void LogRequest(ILogger logger, string host, int statusCode, double elapsedMs);
}
