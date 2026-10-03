using System.Globalization;
using Microsoft.Extensions.Options;
using Onyx.Web.Configuration;

namespace Onyx.Web.Consent;

/// <summary>
/// The visitor's current cookie choices, from the <c>onyx.consent</c> cookie. Choices made under an older policy version
/// count as no decision, so the banner reappears. Non-essential scripts may load only when the matching flag is true.
/// </summary>
public sealed class ConsentState(IHttpContextAccessor httpContextAccessor, IOptions<ConsentOptions> options)
{
    public const string CookieName = "onyx.consent";

    private (bool Parsed, bool HasDecision, bool Analytics, bool Marketing, Guid ConsentId) _state;

    public bool HasDecision => Read().HasDecision;

    public bool AnalyticsAllowed => Read().HasDecision && Read().Analytics;

    public bool MarketingAllowed => Read().HasDecision && Read().Marketing;

    /// <summary>Existing consent id (kept across policy versions), or a new random one.</summary>
    public Guid ConsentId => Read().ConsentId == Guid.Empty ? Guid.NewGuid() : Read().ConsentId;

    public static string Format(string policyVersion, bool analytics, bool marketing, Guid consentId) =>
        string.Create(CultureInfo.InvariantCulture, $"{policyVersion}.{(analytics ? 1 : 0)}{(marketing ? 1 : 0)}.{consentId:N}");

    private (bool Parsed, bool HasDecision, bool Analytics, bool Marketing, Guid ConsentId) Read()
    {
        if (_state.Parsed)
        {
            return _state;
        }

        _state = (true, false, false, false, Guid.Empty);
        var value = httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        var parts = value?.Split('.');
        if (parts is { Length: 3 } && parts[1].Length == 2 && Guid.TryParseExact(parts[2], "N", out var consentId))
        {
            var current = parts[0] == options.Value.PolicyVersion;
            _state = (true, current, parts[1][0] == '1', parts[1][1] == '1', consentId);
        }

        return _state;
    }
}
