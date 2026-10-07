using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Onyx.Infrastructure.Outbound;

namespace Onyx.API.IntegrationTests.Outbound;

/// <summary>
/// A real local HTTP server for outbound tests, plus a fake DNS and a connector. The client's guard runs unchanged: only after
/// it has vetted a (fake) public address does the connector route the connection to this loopback server.
/// </summary>
public sealed class OutboundTestServer : IAsyncDisposable
{
    public const string PublicAddress = "93.184.215.14";

    private readonly WebApplication _app;

    private OutboundTestServer(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    public int Port { get; }

    /// <summary>Response bodies by path (default 200, application/rss+xml).</summary>
    public ConcurrentDictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> RequestedPaths { get; } = new();

    public static async Task<OutboundTestServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        // The test server answers for the fake public host names the client sends (host filtering would reject them).
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        OutboundTestServer? server = null;
        app.Run(async context => await server!.HandleAsync(context));
        await app.StartAsync();
        var port = new Uri(app.Urls.Single()).Port;
        server = new OutboundTestServer(app, port);
        return server;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        RequestedPaths.Enqueue(path + context.Request.QueryString);
        var token = context.RequestAborted;
        switch (path)
        {
            case "/redirect-to-private":
                context.Response.Redirect("http://internal.publisher-sample.com/feed.xml");
                return;
            case "/redirect-to-metadata":
                context.Response.Redirect("http://169.254.169.254/latest/meta-data/");
                return;
            case "/redirect-to-file":
                context.Response.Redirect("file:///etc/passwd");
                return;
            case "/redirect-loop":
                context.Response.Redirect("/redirect-loop");
                return;
            case "/redirect-ok":
                context.Response.Redirect("/feed.xml");
                return;
            case "/large":
                context.Response.ContentLength = 3 * 1_024 * 1_024;
                await context.Response.Body.WriteAsync(new byte[3 * 1_024 * 1_024], token);
                return;
            case "/large-chunked":
                for (var chunk = 0; chunk < 48; chunk++)
                {
                    await context.Response.Body.WriteAsync(new byte[64 * 1_024], token);
                }

                return;
            case "/gzip-bomb":
                context.Response.Headers.ContentEncoding = "gzip";
                await context.Response.Body.WriteAsync(GzipOfZeros(8 * 1_024 * 1_024), token);
                return;
            case "/slow":
                await context.Response.Body.WriteAsync("<rss"u8.ToArray(), token);
                await context.Response.Body.FlushAsync(token);
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                return;
            case "/status-500":
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
        }

        if (Bodies.TryGetValue(path, out var body))
        {
            context.Response.ContentType = path.EndsWith(".txt", StringComparison.Ordinal) ? "text/plain" : "application/rss+xml";
            await context.Response.WriteAsync(body, token);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }

    private static byte[] GzipOfZeros(int size)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(new byte[size]);
        }

        return output.ToArray();
    }
}

/// <summary>Host → answers; a host with several queued answers returns the next one per lookup (DNS rebinding).</summary>
public sealed class FakeDnsResolver : IOutboundDnsResolver
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<IPAddress[]>> _answers = new(StringComparer.OrdinalIgnoreCase);

    public int Lookups { get; private set; }

    public FakeDnsResolver Map(string host, params string[] addresses)
    {
        _answers.GetOrAdd(host, _ => new ConcurrentQueue<IPAddress[]>()).Enqueue(addresses.Select(IPAddress.Parse).ToArray());
        return this;
    }

    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        Lookups++;
        if (!_answers.TryGetValue(host, out var queue) || queue.IsEmpty)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        // The last answer sticks; earlier ones are consumed one per lookup.
        if (queue.Count > 1 && queue.TryDequeue(out var next))
        {
            return Task.FromResult(next);
        }

        queue.TryPeek(out var last);
        return Task.FromResult(last!);
    }
}

/// <summary>Records every address the client asked to connect to and routes the connection to the local test server.</summary>
public sealed class LoopbackConnector(int port) : IOutboundConnector
{
    public ConcurrentQueue<IPAddress> Connected { get; } = new();

    public async ValueTask<Stream> ConnectAsync(IPAddress address, int port1, CancellationToken cancellationToken)
    {
        Connected.Enqueue(address);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }
}
