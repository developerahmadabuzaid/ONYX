using System.Text.RegularExpressions;

namespace Onyx.Infrastructure.Outbound;

/// <summary>
/// The robots.txt rules that apply to one user agent (RFC 9309): the groups naming the agent's product token, else the
/// <c>*</c> groups; the longest matching <c>Allow</c>/<c>Disallow</c> path wins and <c>Allow</c> wins a tie; <c>*</c> and a
/// trailing <c>$</c> are supported. No rules means everything is allowed.
/// </summary>
public sealed class RobotsTxtRules
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    private readonly IReadOnlyList<(bool Allow, string Pattern, Regex Matcher)> _rules;

    private RobotsTxtRules(IReadOnlyList<(bool Allow, string Pattern, Regex Matcher)> rules)
    {
        _rules = rules;
    }

    public static RobotsTxtRules AllowAll { get; } = new([]);

    public static RobotsTxtRules Parse(string? content, string productToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return AllowAll;
        }

        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules)>();
        (List<string> Agents, List<(bool Allow, string Path)> Rules)? current = null;
        var lastWasAgent = false;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim().ToLowerInvariant();
            var value = line[(separator + 1)..].Trim();
            if (key == "user-agent")
            {
                if (current is null || !lastWasAgent)
                {
                    current = ([], []);
                    groups.Add(current.Value);
                }

                current.Value.Agents.Add(value);
                lastWasAgent = true;
                continue;
            }

            lastWasAgent = false;
            if (current is null || key is not ("allow" or "disallow") || value.Length == 0)
            {
                continue;
            }

            current.Value.Rules.Add((key == "allow", value));
        }

        var matching = groups.Where(group => group.Agents.Any(agent => string.Equals(agent, productToken, StringComparison.OrdinalIgnoreCase))).ToList();
        if (matching.Count == 0)
        {
            matching = groups.Where(group => group.Agents.Contains("*", StringComparer.Ordinal)).ToList();
        }

        return new RobotsTxtRules(matching.SelectMany(group => group.Rules).Select(rule => (rule.Allow, rule.Path, ToRegex(rule.Path))).ToList());
    }

    public bool IsAllowed(string pathAndQuery)
    {
        ArgumentNullException.ThrowIfNull(pathAndQuery);
        var path = pathAndQuery.Length == 0 ? "/" : pathAndQuery;
        (bool Allow, int Length)? best = null;
        foreach (var (allow, pattern, matcher) in _rules)
        {
            bool matches;
            try
            {
                matches = matcher.IsMatch(path);
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological pattern is treated as matching a Disallow (the cautious reading).
                matches = !allow;
            }

            if (!matches)
            {
                continue;
            }

            if (best is null || pattern.Length > best.Value.Length || (pattern.Length == best.Value.Length && allow))
            {
                best = (allow, pattern.Length);
            }
        }

        return best?.Allow ?? true;
    }

    private static Regex ToRegex(string pattern)
    {
        var anchored = pattern.EndsWith('$');
        var body = Regex.Escape(anchored ? pattern[..^1] : pattern).Replace(@"\*", ".*", StringComparison.Ordinal);
        return new Regex($"^{body}{(anchored ? "$" : string.Empty)}", RegexOptions.CultureInvariant | RegexOptions.Singleline, MatchTimeout);
    }
}
