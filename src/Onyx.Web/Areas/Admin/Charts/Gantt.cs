using System.Globalization;
using Microsoft.AspNetCore.Html;

namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>
/// A timeline (Gantt) drawn at a fixed number of pixels per day inside a horizontally scrolling area, so nothing is stretched:
/// month headers, sprint bands, today, milestones, bars with real progress and "blocks" connectors. Row labels are HTML next to
/// it at the same row height. Dates run right-to-left in Arabic. Only dates that exist are drawn.
/// </summary>
public static class Gantt
{
    public const int RowHeight = 40;
    private const int HeaderHeight = 46;
    private const int MaxDays = 730;

    // A short range is spread over at least this width, so a few weeks do not shrink into a narrow strip.
    private const int MinWidth = 880;

    // A month name with its year needs about this much room; a shorter first or last month is drawn without a label.
    private const int MonthLabelWidth = 72;

    public static IHtmlContent Render(
        IReadOnlyList<GanttRow> rows,
        IReadOnlyList<(int From, int To)> dependencies,
        IReadOnlyList<GanttBand> bands,
        IReadOnlyList<GanttMarker> markers,
        DateOnly today,
        int pixelsPerDay,
        bool rtl,
        CultureInfo culture,
        string summary,
        string todayLabel)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(culture);

        var dates = rows.SelectMany(row => new[] { row.Start, row.End }).Where(date => date.HasValue).Select(date => date!.Value)
            .Concat(bands.SelectMany(band => new[] { band.Start, band.End }))
            .Concat(markers.Select(marker => marker.Date))
            .Append(today)
            .ToList();
        var first = dates.Min().AddDays(-3);
        var last = dates.Max().AddDays(7);
        if (last.DayNumber - first.DayNumber > MaxDays)
        {
            last = first.AddDays(MaxDays);
        }

        var days = last.DayNumber - first.DayNumber + 1;
        var dayWidth = Math.Max(pixelsPerDay, (int)Math.Ceiling(MinWidth / (double)days));
        var width = days * dayWidth;
        var height = HeaderHeight + (Math.Max(rows.Count, 1) * RowHeight);
        double Edge(DateOnly date) => Math.Clamp(date.DayNumber - first.DayNumber, 0, days) * (double)dayWidth;
        double Mirror(double x) => rtl ? width - x : x;
        (double X, double Width) Span(DateOnly from, DateOnly to)
        {
            var start = Edge(from);
            var end = Edge(to.AddDays(1));
            return (rtl ? width - end : start, Math.Max(end - start, dayWidth));
        }

        double Middle(int row) => HeaderHeight + (row * RowHeight) + (RowHeight / 2.0);

        var builder = new HtmlContentBuilder();
        builder.AppendHtml("<div class=\"gantt chart\"><ol class=\"gantt__labels\" role=\"list\">");
        foreach (var row in rows)
        {
            builder.AppendHtml($"<li class=\"gantt__label{(row.Depth > 0 ? " is-child" : string.Empty)}\">");
            if (row.Key is not null)
            {
                builder.AppendHtml("<span class=\"d-key\" dir=\"ltr\">");
                builder.Append(row.Key);
                builder.AppendHtml("</span>");
            }

            if (row.Url is not null)
            {
                builder.AppendHtml($"<a class=\"gantt__title\" dir=\"auto\" href=\"{Chart.Attr(row.Url)}\">");
                builder.Append(row.Label);
                builder.AppendHtml("</a>");
            }
            else
            {
                builder.AppendHtml("<span class=\"gantt__title\" dir=\"auto\">");
                builder.Append(row.Label);
                builder.AppendHtml("</span>");
            }

            if (row.Meta is not null)
            {
                builder.AppendHtml("<span class=\"gantt__meta\">");
                builder.Append(row.Meta);
                builder.AppendHtml("</span>");
            }

            builder.AppendHtml("</li>");
        }

        builder.AppendHtml($"</ol><div class=\"gantt__scroll\" tabindex=\"0\" role=\"region\" aria-label=\"{Chart.Attr(summary)}\" data-gantt-scroll>");
        builder.AppendHtml($"<svg class=\"gantt__svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\" aria-hidden=\"true\" focusable=\"false\">");

        // Rows, week lines and month headers.
        for (var index = 0; index < rows.Count; index++)
        {
            if (index % 2 == 1)
            {
                builder.AppendHtml($"<rect class=\"gantt__stripe\" x=\"0\" y=\"{HeaderHeight + (index * RowHeight)}\" width=\"{width}\" height=\"{RowHeight}\" />");
            }
        }

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var x = Mirror(Edge(date));
            if (date.DayOfWeek == DayOfWeek.Monday)
            {
                builder.AppendHtml($"<line class=\"gantt__week\" x1=\"{Chart.Num(x)}\" x2=\"{Chart.Num(x)}\" y1=\"24\" y2=\"{height}\" />");
            }

            if (date.Day == 1 || date == first)
            {
                builder.AppendHtml($"<line class=\"gantt__month\" x1=\"{Chart.Num(x)}\" x2=\"{Chart.Num(x)}\" y1=\"0\" y2=\"{height}\" />");
                var nextMonth = new DateOnly(date.Year, date.Month, 1).AddMonths(1);
                var room = (Math.Min(nextMonth.DayNumber, last.DayNumber + 1) - date.DayNumber) * dayWidth;
                if (room >= MonthLabelWidth)
                {
                    builder.AppendHtml($"<text class=\"gantt__month-label\" x=\"{Chart.Num(rtl ? x - 6 : x + 6)}\" y=\"17\">");
                    builder.Append(date.ToDateTime(TimeOnly.MinValue).ToString("MMM yyyy", culture));
                    builder.AppendHtml("</text>");
                }
            }
        }

        foreach (var band in bands)
        {
            var (x, bandWidth) = Span(band.Start, band.End);
            builder.AppendHtml($"<rect class=\"gantt__band\" x=\"{Chart.Num(x)}\" y=\"24\" width=\"{Chart.Num(bandWidth)}\" height=\"{height - 24}\" />");
            builder.AppendHtml($"<text class=\"gantt__band-label\" x=\"{Chart.Num(rtl ? x + bandWidth - 6 : x + 6)}\" y=\"39\">");
            builder.Append(band.Label);
            builder.AppendHtml("</text>");
        }

        foreach (var marker in markers)
        {
            var x = Mirror(Edge(marker.Date) + (dayWidth / 2.0));
            builder.AppendHtml($"<line class=\"gantt__marker tone-{marker.Tone}\" x1=\"{Chart.Num(x)}\" x2=\"{Chart.Num(x)}\" y1=\"24\" y2=\"{height}\" />");
            builder.AppendHtml($"<path class=\"gantt__diamond tone-{marker.Tone}\" d=\"M{Chart.Num(x)},22 l6,6 l-6,6 l-6,-6 Z\" data-tip=\"{Chart.Attr(marker.Label)}\" />");
        }

        var todayX = Mirror(Edge(today) + (dayWidth / 2.0));
        builder.AppendHtml($"<line class=\"gantt__today\" x1=\"{Chart.Num(todayX)}\" x2=\"{Chart.Num(todayX)}\" y1=\"24\" y2=\"{height}\" data-tip=\"{Chart.Attr(todayLabel)}\" />");

        // Dependencies first, so bars are drawn on top of the connectors.
        foreach (var (from, to) in dependencies)
        {
            if (from < 0 || to < 0 || from >= rows.Count || to >= rows.Count
                || (rows[from].End ?? rows[from].Start) is not { } blockerEnd || (rows[to].Start ?? rows[to].End) is not { } blockedStart)
            {
                continue;
            }

            var x1 = Edge(blockerEnd.AddDays(1));
            var x2 = Edge(blockedStart);
            var (y1, y2) = (Middle(from), Middle(to));
            var path = x2 - 8 >= x1 + 8
                ? $"M{Chart.Num(Mirror(x1))},{Chart.Num(y1)} H{Chart.Num(Mirror(x1 + 8))} V{Chart.Num(y2)} H{Chart.Num(Mirror(x2))}"
                : $"M{Chart.Num(Mirror(x1))},{Chart.Num(y1)} H{Chart.Num(Mirror(x1 + 8))} V{Chart.Num((y1 + y2) / 2)} H{Chart.Num(Mirror(x2 - 8))} V{Chart.Num(y2)} H{Chart.Num(Mirror(x2))}";
            builder.AppendHtml($"<path class=\"gantt__dependency\" d=\"{path}\" />");
            var tip = Mirror(x2);
            builder.AppendHtml($"<path class=\"gantt__arrow\" d=\"M{Chart.Num(tip)},{Chart.Num(y2)} l{(rtl ? 6 : -6)},-4 v8 Z\" />");
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var top = HeaderHeight + (index * RowHeight);
            var open = row.Url is null ? string.Empty : $"<a class=\"gantt__link\" href=\"{Chart.Attr(row.Url)}\">";
            var close = row.Url is null ? string.Empty : "</a>";
            if (row.Start is { } start && row.End is { } end && end >= start)
            {
                var (x, barWidth) = Span(start, end);
                builder.AppendHtml(open);
                builder.AppendHtml($"<rect class=\"gantt__bar tone-{row.Tone}\" x=\"{Chart.Num(x)}\" y=\"{top + 10}\" width=\"{Chart.Num(barWidth)}\" height=\"20\" rx=\"6\" data-tip=\"{Chart.Attr(row.Tip)}\" />");
                if (row.Progress is { } progress && progress > 0)
                {
                    var done = barWidth * Math.Clamp(progress, 0, 1);
                    builder.AppendHtml($"<rect class=\"gantt__done tone-{row.Tone}\" x=\"{Chart.Num(rtl ? x + barWidth - done : x)}\" y=\"{top + 10}\" width=\"{Chart.Num(done)}\" height=\"20\" rx=\"6\" data-tip=\"{Chart.Attr(row.Tip)}\" />");
                }

                builder.AppendHtml(close);
            }
            else if ((row.End ?? row.Start) is { } single)
            {
                var x = Mirror(Edge(single) + (dayWidth / 2.0));
                builder.AppendHtml(open);
                builder.AppendHtml($"<path class=\"gantt__point tone-{row.Tone}\" d=\"M{Chart.Num(x)},{top + 10} l10,10 l-10,10 l-10,-10 Z\" data-tip=\"{Chart.Attr(row.Tip)}\" />");
                builder.AppendHtml(close);
            }
        }

        builder.AppendHtml($"<text class=\"gantt__today-label\" x=\"{Chart.Num(rtl ? todayX - 4 : todayX + 4)}\" y=\"{HeaderHeight - 4}\">");
        builder.Append(todayLabel);
        builder.AppendHtml("</text></svg></div></div>");
        return builder;
    }
}
