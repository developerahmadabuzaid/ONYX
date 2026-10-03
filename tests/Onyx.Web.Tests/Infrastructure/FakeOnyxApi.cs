using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Onyx.Contracts.Common;

namespace Onyx.Web.Tests.Infrastructure;

/// <summary>Stands in for Onyx.API at the HTTP boundary: Onyx.Web is tested exactly as it talks to the real API.</summary>
public sealed class FakeOnyxApi : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Func<RecordedApiRequest, HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public ConcurrentQueue<RecordedApiRequest> Requests { get; } = new();

    public void On(HttpMethod method, string path, Func<RecordedApiRequest, HttpResponseMessage> respond) =>
        _routes[$"{method.Method} {path}"] = respond;

    public IReadOnlyList<RecordedApiRequest> RequestsTo(string path) => Requests.Where(request => request.Path == path).ToList();

    public static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(value) };

    public static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    public static HttpResponseMessage Problem(HttpStatusCode status, string code) =>
        new(status)
        {
            Content = JsonContent.Create(
                new ApiProblem { Status = (int)status, Code = code, Title = code },
                mediaType: new MediaTypeHeaderValue("application/problem+json")),
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedApiRequest(
            request.Method,
            request.RequestUri!.AbsolutePath.TrimStart('/'),
            request.RequestUri.Query,
            request.Headers.Authorization?.Parameter,
            request.Headers.TryGetValues("X-Forwarded-For", out var forwarded) ? forwarded.Single() : null,
            request.Headers.AcceptLanguage.ToString(),
            body);
        Requests.Enqueue(recorded);

        return _routes.TryGetValue($"{request.Method.Method} {recorded.Path}", out var respond)
            ? respond(recorded)
            : Problem(HttpStatusCode.NotFound, "Test.NoRoute");
    }
}
