using System.Globalization;
using System.Text.RegularExpressions;

namespace Onyx.Application.Jobs;

/// <summary>
/// In-house schedule expressions (no scheduling library):
/// <list type="bullet">
/// <item><c>every:30s</c>, <c>every:5m</c>, <c>every:2h</c> — fixed interval (minimum 10 seconds).</item>
/// <item><c>daily@03:00</c> — once a day at the given UTC time.</item>
/// </list>
/// </summary>
public sealed partial class JobSchedule
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(10);

    private readonly TimeSpan? _interval;
    private readonly TimeSpan? _dailyAtUtc;

    private JobSchedule(string expression, TimeSpan? interval, TimeSpan? dailyAtUtc)
    {
        Expression = expression;
        _interval = interval;
        _dailyAtUtc = dailyAtUtc;
    }

    public string Expression { get; }

    public static JobSchedule Parse(string expression) =>
        TryParse(expression, out var schedule)
            ? schedule
            : throw new FormatException($"Invalid job schedule '{expression}'. Use 'every:<n>s|m|h' or 'daily@HH:mm'.");

    public static bool TryParse(string? expression, out JobSchedule schedule)
    {
        schedule = null!;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var trimmed = expression.Trim();
        var every = EveryPattern().Match(trimmed);
        if (every.Success)
        {
            var amount = int.Parse(every.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var interval = every.Groups["unit"].Value switch
            {
                "s" => TimeSpan.FromSeconds(amount),
                "m" => TimeSpan.FromMinutes(amount),
                _ => TimeSpan.FromHours(amount),
            };

            if (interval < MinimumInterval || interval > TimeSpan.FromDays(7))
            {
                return false;
            }

            schedule = new JobSchedule(trimmed, interval, null);
            return true;
        }

        var daily = DailyPattern().Match(trimmed);
        if (daily.Success)
        {
            var hours = int.Parse(daily.Groups["hours"].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(daily.Groups["minutes"].Value, CultureInfo.InvariantCulture);
            if (hours > 23 || minutes > 59)
            {
                return false;
            }

            schedule = new JobSchedule(trimmed, null, new TimeSpan(hours, minutes, 0));
            return true;
        }

        return false;
    }

    /// <summary>The next run strictly after <paramref name="afterUtc"/>.</summary>
    public DateTimeOffset GetNextOccurrence(DateTimeOffset afterUtc)
    {
        var after = afterUtc.ToUniversalTime();
        if (_interval is { } interval)
        {
            return after.Add(interval);
        }

        var candidate = new DateTimeOffset(after.Date, TimeSpan.Zero).Add(_dailyAtUtc!.Value);
        return candidate > after ? candidate : candidate.AddDays(1);
    }

    [GeneratedRegex(@"^every:(?<amount>[1-9]\d{0,5})(?<unit>[smh])$", RegexOptions.CultureInvariant)]
    private static partial Regex EveryPattern();

    [GeneratedRegex(@"^daily@(?<hours>\d{2}):(?<minutes>\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DailyPattern();
}
