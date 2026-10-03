using System.Text.RegularExpressions;

namespace Onyx.Web.Tests.Infrastructure;

public static partial class BrowserExtensions
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> GetPathAsync(this HttpClient client, string path) =>
        client.GetAsync(new Uri(path, UriKind.Relative), Token);

    public static async Task<string> GetHtmlAsync(this HttpClient client, string path)
    {
        using var response = await client.GetPathAsync(path);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Token);
    }

    /// <summary>Loads the form page (for its antiforgery token), then posts the fields to <paramref name="action"/>.</summary>
    public static async Task<HttpResponseMessage> SubmitFormAsync(this HttpClient client, string page, string action, IDictionary<string, string> fields)
    {
        var html = await client.GetHtmlAsync(page);
        return await client.PostFormAsync(action, fields, AntiforgeryToken(html));
    }

    public static Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string action, IDictionary<string, string> fields, string? antiforgeryToken)
    {
        var values = new Dictionary<string, string>(fields);
        if (antiforgeryToken is not null)
        {
            values["__RequestVerificationToken"] = antiforgeryToken;
        }

        return client.PostAsync(new Uri(action, UriKind.Relative), new FormUrlEncodedContent(values), Token);
    }

    public static string AntiforgeryToken(string html) => AntiforgeryField().Match(html) is { Success: true } match
        ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value)
        : throw new InvalidOperationException("No antiforgery token in the page.");

    public static string? Location(this HttpResponseMessage response) => response.Headers.Location?.OriginalString;

    public static IReadOnlyList<string> SetCookies(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Where(value => value.StartsWith(name + "=", StringComparison.Ordinal)).ToList()
            : [];

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
