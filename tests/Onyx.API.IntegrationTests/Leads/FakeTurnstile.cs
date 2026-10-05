using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>
/// Stands in for Cloudflare siteverify behind the real <c>TurnstileCaptchaVerifier</c>. Tokens are <c>ok:{action}:{hostname}[:nonce]</c>
/// (solved), <c>fail</c> (rejected) or <c>stale:{action}:{hostname}</c> (solved ten minutes ago). Like the real service, a token is
/// accepted once only. Every request is recorded so tests can check the secret and the visitor IP were sent.
/// </summary>
public sealed class FakeTurnstile(TimeProvider time) : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, bool> _redeemed = new(StringComparer.Ordinal);

    public ConcurrentQueue<IReadOnlyDictionary<string, string>> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var form = body.Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair.Length > 1 ? pair[1] : string.Empty), StringComparer.Ordinal);
        Requests.Enqueue(form);

        var token = form.GetValueOrDefault("response") ?? string.Empty;
        var parts = token.Split(':');
        object reply;
        if (parts.Length >= 3 && parts[0] is "ok" or "stale" && _redeemed.TryAdd(token, true))
        {
            var issued = parts[0] == "stale" ? time.GetUtcNow().AddMinutes(-10) : time.GetUtcNow();
            reply = new Dictionary<string, object>
            {
                ["success"] = true,
                ["challenge_ts"] = issued.ToString("O"),
                ["hostname"] = parts[2],
                ["action"] = parts[1],
                ["error-codes"] = Array.Empty<string>(),
            };
        }
        else
        {
            reply = new Dictionary<string, object>
            {
                ["success"] = false,
                ["error-codes"] = new[] { parts.Length >= 3 ? "timeout-or-duplicate" : "invalid-input-response" },
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(reply), Encoding.UTF8, "application/json"),
        };
    }
}
