using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Content.SiteContent;

/// <summary>
/// Applies an editor override to a code-owned site page. Only text, visibility and order change: the key, type, paths, anchors,
/// item link targets, icons and capability flags always come from the code-owned definition.
/// </summary>
public static class SitePageOverlay
{
    /// <summary>The key used for a section in overrides: its id, or <c>#index</c> when it has none.</summary>
    public static string SectionKey(SitePageSection section, int index)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.Id ?? $"#{index}";
    }

    public static string ItemKey(SitePageItem item, int index)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Id ?? $"#{index}";
    }

    public static SitePageDefinition Apply(SitePageDefinition page, PageOverride? overrides)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (overrides is null)
        {
            return page;
        }

        var translations = page.Translations.ToDictionary(
            pair => pair.Key,
            pair => Apply(pair.Value, overrides.Translations.GetValueOrDefault(pair.Key), overrides.Settings),
            StringComparer.Ordinal);
        return page with { Translations = translations };
    }

    private static SitePageText Apply(SitePageText text, PageTextOverride? overrides, PageOverrideSettings settings)
    {
        var sections = text.Sections
            .Select((section, index) => (Key: SectionKey(section, index), Index: index, Section: Apply(section, overrides?.Sections.GetValueOrDefault(SectionKey(section, index)))))
            .Where(entry => !settings.HiddenSections.Contains(entry.Key, StringComparer.Ordinal))
            .OrderBy(entry => OrderOf(settings.SectionOrder, entry.Key))
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Section)
            .ToList();

        return text with
        {
            Title = Pick(overrides?.Title, text.Title),
            Description = Pick(overrides?.Description, text.Description),
            Nav = Pick(overrides?.Nav, text.Nav),
            Summary = PickOptional(overrides?.Summary, text.Summary),
            Eyebrow = PickOptional(overrides?.Eyebrow, text.Eyebrow),
            H1 = Pick(overrides?.H1, text.H1),
            Lead = PickOptional(overrides?.Lead, text.Lead),
            CtaTitle = PickOptional(overrides?.CtaTitle, text.CtaTitle),
            CtaText = PickOptional(overrides?.CtaText, text.CtaText),
            Sections = sections,
        };
    }

    private static SitePageSection Apply(SitePageSection section, SectionOverride? overrides)
    {
        if (overrides is null)
        {
            return section;
        }

        return section with
        {
            Eyebrow = PickOptional(overrides.Eyebrow, section.Eyebrow),
            Heading = PickOptional(overrides.Heading, section.Heading),
            Lead = PickOptional(overrides.Lead, section.Lead),
            Note = PickOptional(overrides.Note, section.Note),
            Paragraphs = PickList(overrides.Paragraphs, section.Paragraphs),
            Items = section.Items.Select((item, index) => Apply(item, overrides.Items.GetValueOrDefault(ItemKey(item, index)))).ToList(),
        };
    }

    private static SitePageItem Apply(SitePageItem item, ItemOverride? overrides)
    {
        if (overrides is null)
        {
            return item;
        }

        return item with
        {
            Title = Pick(overrides.Title, item.Title),
            Text = PickOptional(overrides.Text, item.Text),
            Points = PickList(overrides.Points, item.Points),
            Tags = PickList(overrides.Tags, item.Tags),
            Deliverables = PickList(overrides.Deliverables, item.Deliverables),
            Involvement = PickList(overrides.Involvement, item.Involvement),
        };
    }

    private static int OrderOf(List<string> order, string key)
    {
        var position = order.IndexOf(key);
        return position < 0 ? int.MaxValue : position;
    }

    private static string Pick(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? PickOptional(string? value, string? fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static IReadOnlyList<string> PickList(List<string>? value, IReadOnlyList<string> fallback)
    {
        var cleaned = value?.Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
        return cleaned is { Count: > 0 } ? cleaned : fallback;
    }
}
