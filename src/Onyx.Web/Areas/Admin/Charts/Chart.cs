using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>
/// Server-rendered SVG charts for the Admin: no JavaScript library, no inline styles (CSP stays <c>'self'</c>), colours from
/// tokens through <c>tone-*</c> classes. Text never sits inside a stretched SVG: axis labels and legends are HTML, so they stay
/// crisp at any width and follow the page direction. Time axes run right-to-left in Arabic. Every value shown is the value given;
/// nothing is smoothed or estimated. Labels are encoded (they can contain user-entered names).
/// </summary>
public static class Chart
{
    private const double PlotWidth = 600;
    private const double PlotHeight = 200;

    public static IHtmlContent Donut(IReadOnlyList<ChartSlice> slices, string centerValue, string centerLabel, string summary)
    {
        ArgumentNullException.ThrowIfNull(slices);
        var total = slices.Sum(slice => Math.Max(slice.Value, 0));
        var visible = slices.Where(slice => slice.Value > 0).ToList();
        var builder = new HtmlContentBuilder();
        builder.AppendHtml($"<div class=\"donut chart\"><svg class=\"donut__svg\" viewBox=\"0 0 42 42\" role=\"img\" aria-label=\"{Attr(summary)}\">");
        builder.AppendHtml("<circle class=\"donut__track\" cx=\"21\" cy=\"21\" r=\"15.9155\" />");
        var offset = 0d;
        foreach (var slice in visible)
        {
            var share = slice.Value / total * 100;
            var length = Math.Max(share - (visible.Count > 1 ? 1 : 0), 0.4);
            builder.AppendHtml(
                $"<circle class=\"donut__seg tone-{slice.Tone}\" cx=\"21\" cy=\"21\" r=\"15.9155\" pathLength=\"100\" stroke-dasharray=\"{Num(length)} {Num(100 - length)}\" " +
                $"stroke-dashoffset=\"{Num(25 - offset)}\" data-tip=\"{Attr($"{slice.Label}: {Num(slice.Value)} ({Math.Round(share)}%)")}\" />");
            offset += share;
        }

        builder.AppendHtml("</svg><div class=\"donut__center\"><strong dir=\"ltr\">");
        builder.Append(centerValue);
        builder.AppendHtml("</strong><span>");
        builder.Append(centerLabel);
        builder.AppendHtml("</span></div></div>");
        return builder;
    }

    public static IHtmlContent Legend(IEnumerable<(string Label, string Tone, string? Value)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var builder = new HtmlContentBuilder();
        builder.AppendHtml("<ul class=\"chart-legend\" role=\"list\">");
        foreach (var (label, tone, value) in items)
        {
            builder.AppendHtml($"<li><span class=\"chart-legend__swatch tone-{tone}\" aria-hidden=\"true\"></span><span class=\"chart-legend__label\">");
            builder.Append(label);
            builder.AppendHtml("</span>");
            if (value is not null)
            {
                builder.AppendHtml("<strong class=\"chart-legend__value\" dir=\"ltr\">");
                builder.Append(value);
                builder.AppendHtml("</strong>");
            }

            builder.AppendHtml("</li>");
        }

        builder.AppendHtml("</ul>");
        return builder;
    }

    /// <summary>
    /// A line chart over evenly spaced points (days, weeks or sprint days). Null values leave a gap (e.g. future days of a burndown).
    /// <paramref name="tips"/> gives one tooltip per point.
    /// </summary>
    public static IHtmlContent Line(IReadOnlyList<string> xLabels, IReadOnlyList<ChartSeries> series, IReadOnlyList<string> tips, bool rtl, string summary, int maxXLabels = 6)
    {
        ArgumentNullException.ThrowIfNull(xLabels);
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(tips);
        var count = xLabels.Count;
        var max = NiceMax(series.SelectMany(line => line.Values).Where(value => value.HasValue).Select(value => value!.Value).DefaultIfEmpty(0).Max());
        double X(int index)
        {
            var share = count <= 1 ? 0.5 : (double)index / (count - 1);
            return (rtl ? 1 - share : share) * PlotWidth;
        }

        static double Y(double value, double top) => PlotHeight - (value / top * PlotHeight);

        var builder = new HtmlContentBuilder();
        builder.AppendHtml("<div class=\"lchart chart\"><div class=\"lchart__y\" aria-hidden=\"true\">");
        foreach (var tick in new[] { max, max / 2, 0 })
        {
            builder.AppendHtml($"<span dir=\"ltr\">{Num(tick)}</span>");
        }

        builder.AppendHtml($"</div><div class=\"lchart__plot\"><svg class=\"lchart__svg\" viewBox=\"0 0 {Num(PlotWidth)} {Num(PlotHeight)}\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{Attr(summary)}\">");
        for (var line = 0; line <= 4; line++)
        {
            var y = PlotHeight * line / 4;
            builder.AppendHtml($"<line class=\"chart__grid\" x1=\"0\" x2=\"{Num(PlotWidth)}\" y1=\"{Num(y)}\" y2=\"{Num(y)}\" vector-effect=\"non-scaling-stroke\" />");
        }

        foreach (var line in series)
        {
            foreach (var segment in Segments(line.Values))
            {
                var points = segment.Select(index => $"{Num(X(index))},{Num(Y(line.Values[index]!.Value, max))}").ToList();
                if (line.Area && segment.Count > 1)
                {
                    var first = X(segment[0]);
                    var last = X(segment[^1]);
                    builder.AppendHtml($"<path class=\"lchart__area tone-{line.Tone}\" d=\"M{Num(first)},{Num(PlotHeight)} L{string.Join(" L", points)} L{Num(last)},{Num(PlotHeight)} Z\" />");
                }

                var dash = line.Dashed ? " is-dashed" : string.Empty;
                var shape = segment.Count == 1
                    ? $"<line class=\"lchart__line tone-{line.Tone}{dash}\" x1=\"{Num(X(segment[0]) - 2)}\" x2=\"{Num(X(segment[0]) + 2)}\" y1=\"{Num(Y(line.Values[segment[0]]!.Value, max))}\" y2=\"{Num(Y(line.Values[segment[0]]!.Value, max))}\" vector-effect=\"non-scaling-stroke\" />"
                    : $"<polyline class=\"lchart__line tone-{line.Tone}{dash}\" points=\"{string.Join(' ', points)}\" vector-effect=\"non-scaling-stroke\" />";
                builder.AppendHtml(shape);
            }
        }

        var width = count <= 1 ? PlotWidth : PlotWidth / (count - 1);
        for (var index = 0; index < count; index++)
        {
            var x = Math.Clamp(X(index) - (width / 2), 0, PlotWidth);
            builder.AppendHtml($"<rect class=\"chart__hit\" x=\"{Num(x)}\" y=\"0\" width=\"{Num(Math.Min(width, PlotWidth - x))}\" height=\"{Num(PlotHeight)}\" data-tip=\"{Attr(index < tips.Count ? tips[index] : xLabels[index])}\" />");
        }

        builder.AppendHtml("</svg></div><div class=\"lchart__x\" aria-hidden=\"true\">");
        foreach (var index in LabelIndexes(count, maxXLabels))
        {
            builder.AppendHtml("<span>");
            builder.Append(xLabels[index]);
            builder.AppendHtml("</span>");
        }

        builder.AppendHtml("</div></div>");
        return builder;
    }

    /// <summary>
    /// Horizontal bars, one row per category with an HTML label and total. Stacked: segments share one bar; otherwise each series
    /// gets its own thin bar in the row. All rows share one scale so they can be compared.
    /// </summary>
    public static IHtmlContent Bars(IReadOnlyList<string> categories, IReadOnlyList<ChartSeries> series, bool stacked, bool rtl, string summary, IReadOnlyList<string>? hrefs = null)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(series);
        double ValueAt(ChartSeries line, int index) => index < line.Values.Count ? line.Values[index] ?? 0 : 0;
        var max = NiceMax(Enumerable.Range(0, categories.Count)
            .Select(index => stacked ? series.Sum(line => ValueAt(line, index)) : series.Max(line => ValueAt(line, index)))
            .DefaultIfEmpty(0)
            .Max());

        var builder = new HtmlContentBuilder();
        builder.AppendHtml($"<ul class=\"hbars chart\" role=\"list\" aria-label=\"{Attr(summary)}\">");
        for (var index = 0; index < categories.Count; index++)
        {
            builder.AppendHtml("<li class=\"hbars__row\"><span class=\"hbars__label\" dir=\"auto\">");
            if (hrefs is not null && index < hrefs.Count && !string.IsNullOrEmpty(hrefs[index]))
            {
                builder.AppendHtml($"<a href=\"{Attr(hrefs[index])}\">");
                builder.Append(categories[index]);
                builder.AppendHtml("</a>");
            }
            else
            {
                builder.Append(categories[index]);
            }

            var rowHeight = stacked ? 10 : (series.Count * 8) - 2;
            builder.AppendHtml($"</span><svg class=\"hbars__bar{(stacked ? string.Empty : " is-grouped")}\" viewBox=\"0 0 100 {rowHeight}\" preserveAspectRatio=\"none\" aria-hidden=\"true\">");
            builder.AppendHtml($"<rect class=\"hbars__track\" x=\"0\" y=\"0\" width=\"100\" height=\"{rowHeight}\" />");
            var cumulative = 0d;
            for (var line = 0; line < series.Count; line++)
            {
                var value = ValueAt(series[line], index);
                var length = value / max * 100;
                var start = stacked ? cumulative : 0;
                var x = rtl ? 100 - start - length : start;
                var y = stacked ? 0 : line * 8;
                var height = stacked ? 10 : 6;
                if (value > 0)
                {
                    builder.AppendHtml($"<rect class=\"hbars__seg tone-{series[line].Tone}\" x=\"{Num(x)}\" y=\"{y}\" width=\"{Num(length)}\" height=\"{height}\" data-tip=\"{Attr($"{categories[index]} · {series[line].Label}: {Num(value)}")}\" />");
                }

                cumulative += length;
            }

            builder.AppendHtml("</svg><span class=\"hbars__value\" dir=\"ltr\">");
            builder.Append(stacked
                ? Num(series.Sum(line => ValueAt(line, index)))
                : string.Join(" / ", series.Select(line => Num(ValueAt(line, index)))));
            builder.AppendHtml("</span></li>");
        }

        builder.AppendHtml("</ul>");
        return builder;
    }

    /// <summary>Vertical grouped columns (e.g. committed vs completed per sprint), oldest first in reading order.</summary>
    public static IHtmlContent Columns(IReadOnlyList<string> categories, IReadOnlyList<ChartSeries> series, bool rtl, string summary)
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(series);
        var max = NiceMax(series.SelectMany(line => line.Values).Select(value => value ?? 0).DefaultIfEmpty(0).Max());
        var builder = new HtmlContentBuilder();
        builder.AppendHtml($"<div class=\"vbars chart\" role=\"img\" aria-label=\"{Attr(summary)}\">");
        for (var index = 0; index < categories.Count; index++)
        {
            builder.AppendHtml("<div class=\"vbars__group\"><svg class=\"vbars__svg\" viewBox=\"0 0 24 100\" preserveAspectRatio=\"none\" aria-hidden=\"true\">");
            var width = 20.0 / Math.Max(series.Count, 1);
            for (var line = 0; line < series.Count; line++)
            {
                var value = index < series[line].Values.Count ? series[line].Values[index] ?? 0 : 0;
                var height = value / max * 100;
                var slot = rtl ? series.Count - 1 - line : line;
                builder.AppendHtml($"<rect class=\"vbars__bar tone-{series[line].Tone}\" x=\"{Num(2 + (slot * width))}\" y=\"{Num(100 - height)}\" width=\"{Num(width - 1)}\" height=\"{Num(height)}\" data-tip=\"{Attr($"{categories[index]} · {series[line].Label}: {Num(value)}")}\" />");
            }

            builder.AppendHtml("</svg><span class=\"vbars__value\">");
            builder.Append(string.Join(" / ", series.Select(line => Num(index < line.Values.Count ? line.Values[index] ?? 0 : 0))));
            builder.AppendHtml("</span><span class=\"vbars__label\">");
            builder.Append(categories[index]);
            builder.AppendHtml("</span></div>");
        }

        builder.AppendHtml("</div>");
        return builder;
    }

    /// <summary>A small trend line for a KPI card (decorative: the card states the numbers in text).</summary>
    public static IHtmlContent Sparkline(IReadOnlyList<double> values, string tone, bool rtl)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count < 2)
        {
            return HtmlString.Empty;
        }

        var max = Math.Max(values.Max(), 1);
        var points = values.Select((value, index) =>
        {
            var share = (double)index / (values.Count - 1);
            return (X: (rtl ? 1 - share : share) * 100, Y: 28 - (value / max * 24));
        }).ToList();
        var line = string.Join(' ', points.Select(point => $"{Num(point.X)},{Num(point.Y)}"));
        return new HtmlString(
            $"<svg class=\"spark tone-{tone}\" viewBox=\"0 0 100 30\" preserveAspectRatio=\"none\" aria-hidden=\"true\" focusable=\"false\">" +
            $"<path class=\"spark__area\" d=\"M{Num(points[0].X)},30 L{string.Join(" L", points.Select(point => $"{Num(point.X)},{Num(point.Y)}"))} L{Num(points[^1].X)},30 Z\" />" +
            $"<polyline class=\"spark__line\" points=\"{line}\" vector-effect=\"non-scaling-stroke\" /></svg>");
    }

    /// <summary>A progress bar; <paramref name="ratio"/> is 0..1 of real counts.</summary>
    public static IHtmlContent Progress(double ratio, string tone, bool rtl, string label)
    {
        var width = Math.Clamp(ratio, 0, 1) * 100;
        return new HtmlString(
            $"<svg class=\"pbar\" viewBox=\"0 0 100 8\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{Attr(label)}\">" +
            "<rect class=\"pbar__track\" x=\"0\" y=\"0\" width=\"100\" height=\"8\" />" +
            $"<rect class=\"pbar__fill tone-{tone}\" x=\"{Num(rtl ? 100 - width : 0)}\" y=\"0\" width=\"{Num(width)}\" height=\"8\" /></svg>");
    }

    /// <summary>A nice axis maximum for counts: at least 4, then 6, 8, 10, 20, 40, 60, 80, 100… so the half mark is a whole number.</summary>
    public static double NiceMax(double value)
    {
        if (value <= 4)
        {
            return 4;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 4, 6, 8, 10 })
        {
            if (step * magnitude >= value)
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }

    internal static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static string Attr(string value) => HtmlEncoder.Default.Encode(value);

    private static IEnumerable<int> LabelIndexes(int count, int max)
    {
        if (count == 0)
        {
            yield break;
        }

        var labels = Math.Min(Math.Max(max, 2), count);
        var previous = -1;
        for (var slot = 0; slot < labels; slot++)
        {
            var index = labels == 1 ? 0 : (int)Math.Round((double)slot * (count - 1) / (labels - 1));
            if (index != previous)
            {
                yield return index;
                previous = index;
            }
        }
    }

    private static List<List<int>> Segments(IReadOnlyList<double?> values)
    {
        var segments = new List<List<int>>();
        List<int>? current = null;
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is null)
            {
                current = null;
                continue;
            }

            if (current is null)
            {
                current = [];
                segments.Add(current);
            }

            current.Add(index);
        }

        return segments;
    }
}
