namespace Onyx.Web.ApiClients;

/// <summary>A streamed API file response. Disposing it releases the underlying HTTP response.</summary>
public sealed class ApiFileStream(HttpResponseMessage response, Stream content) : IAsyncDisposable, IDisposable
{
    public Stream Content { get; } = content;

    public string ContentType => response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

    public long? Length => response.Content.Headers.ContentLength;

    public string? CacheControl => response.Headers.CacheControl?.ToString();

    public string? ETag => response.Headers.ETag?.ToString();

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        response.Dispose();
    }

    public void Dispose()
    {
        Content.Dispose();
        response.Dispose();
    }
}
