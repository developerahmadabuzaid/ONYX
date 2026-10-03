using System.Text.Json;

namespace Onyx.Web.Tests.Infrastructure;

public sealed record RecordedApiRequest(
    HttpMethod Method,
    string Path,
    string Query,
    string? BearerToken,
    string? ForwardedFor,
    string AcceptLanguage,
    string? Body)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public T BodyAs<T>() => JsonSerializer.Deserialize<T>(Body ?? "null", Web)!;
}
