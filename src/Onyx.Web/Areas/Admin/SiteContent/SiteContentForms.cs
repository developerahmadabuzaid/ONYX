using Microsoft.Extensions.Primitives;
using Onyx.Contracts.SiteContent;
using Onyx.Web.Content.SiteContent;
using Onyx.Web.Content.SitePages;

namespace Onyx.Web.Areas.Admin.SiteContent;

/// <summary>
/// Turns the admin editor forms into override documents. Only values that differ from the code-owned default are stored, so
/// untouched fields keep following the code, and clearing a field restores the default. Structure (sections, items, anchors,
/// links) comes from the code-owned page and cannot be changed through these forms.
/// </summary>
public static class SiteContentForms
{
    public const char Separator = '|';

    public static string Field(params string[] parts) => string.Join(Separator, parts);

    // ------------------------------------------------------------------------------------------------ Site pages ---

    public static PageOverride BuildPage(SitePageDefinition page, IReadOnlyDictionary<string, StringValues> form)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(form);
        var translations = new Dictionary<string, PageTextOverride>(StringComparer.Ordinal);
        foreach (var (language, text) in page.Translations)
        {
            var sections = new Dictionary<string, SectionOverride>(StringComparer.Ordinal);
            for (var s = 0; s < text.Sections.Count; s++)
            {
                var section = text.Sections[s];
                var sectionKey = SitePageOverlay.SectionKey(section, s);
                string F(params string[] parts) => Field([language, "s", sectionKey, .. parts]);
                var items = new Dictionary<string, ItemOverride>(StringComparer.Ordinal);
                for (var i = 0; i < section.Items.Count; i++)
                {
                    var item = section.Items[i];
                    var itemKey = SitePageOverlay.ItemKey(item, i);
                    var itemOverride = new ItemOverride
                    {
                        Title = Changed(form, F("i", itemKey, "title"), item.Title),
                        Text = Changed(form, F("i", itemKey, "text"), item.Text),
                        Points = ChangedList(form, F("i", itemKey, "points"), item.Points),
                        Tags = ChangedList(form, F("i", itemKey, "tags"), item.Tags),
                        Deliverables = ChangedList(form, F("i", itemKey, "deliverables"), item.Deliverables),
                        Involvement = ChangedList(form, F("i", itemKey, "involvement"), item.Involvement),
                    };
                    if (!IsEmpty(itemOverride))
                    {
                        items[itemKey] = itemOverride;
                    }
                }

                var sectionOverride = new SectionOverride
                {
                    Eyebrow = Changed(form, F("eyebrow"), section.Eyebrow),
                    Heading = Changed(form, F("heading"), section.Heading),
                    Lead = Changed(form, F("lead"), section.Lead),
                    Note = Changed(form, F("note"), section.Note),
                    Paragraphs = ChangedList(form, F("paragraphs"), section.Paragraphs, paragraphs: true),
                    Items = items,
                };
                if (!IsEmpty(sectionOverride))
                {
                    sections[sectionKey] = sectionOverride;
                }
            }

            string T(string name) => Field(language, name);
            var textOverride = new PageTextOverride
            {
                Title = Changed(form, T("title"), text.Title),
                Description = Changed(form, T("description"), text.Description),
                OgTitle = Blank(Value(form, T("ogTitle"))),
                OgDescription = Blank(Value(form, T("ogDescription"))),
                Nav = Changed(form, T("nav"), text.Nav),
                Summary = Changed(form, T("summary"), text.Summary),
                Eyebrow = Changed(form, T("eyebrow"), text.Eyebrow),
                H1 = Changed(form, T("h1"), text.H1),
                Lead = Changed(form, T("lead"), text.Lead),
                CtaTitle = Changed(form, T("ctaTitle"), text.CtaTitle),
                CtaText = Changed(form, T("ctaText"), text.CtaText),
                Sections = sections,
            };
            translations[language] = textOverride;
        }

        var sectionKeys = page.Translations.Values.First().Sections.Select(SitePageOverlay.SectionKey).ToList();
        return new PageOverride
        {
            Translations = translations,
            Settings = new PageOverrideSettings
            {
                HiddenSections = Values(form, "set|hidden").Where(sectionKeys.Contains).Distinct(StringComparer.Ordinal).ToList(),
                SectionOrder = Order(form, "set|order|", sectionKeys),
                HeroImageMediaId = MediaId(form, "set|heroImageMediaId"),
                SocialImageMediaId = MediaId(form, "set|socialImageMediaId"),
                NoIndex = Values(form, "set|noIndex").Contains("true"),
            },
        };
    }

    // ------------------------------------------------------------------- Homepage / navigation & footer copy ---

    /// <param name="defaults">Language → key → resource-file text.</param>
    /// <param name="homeSettings">True for the homepage document (section visibility, order and images).</param>
    public static CopyOverride BuildCopy(IReadOnlyList<CopyGroup> groups, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> defaults, IReadOnlyDictionary<string, StringValues> form, bool homeSettings)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(form);
        var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (language, values) in defaults)
        {
            var changed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in groups.SelectMany(group => group.Keys))
            {
                var submitted = Value(form, Field(language, key));
                if (SiteCopyCatalog.IsList(key))
                {
                    // Technology lists are edited one per line and stored "|"-separated, as in the resource files.
                    submitted = string.Join('|', Lines(submitted));
                }

                if (Changed(submitted, values.GetValueOrDefault(key)) is { } value)
                {
                    changed[key] = value;
                }
            }

            translations[language] = changed;
        }

        var settings = homeSettings
            ? new CopyOverrideSettings
            {
                HiddenSections = Values(form, "set|hidden").Where(SiteCopyCatalog.HomeHideableSections.Contains).Distinct(StringComparer.Ordinal).ToList(),
                SectionOrder = Order(form, "set|order|", SiteCopyCatalog.HomeMovableSections),
                WhyImageMediaId = MediaId(form, "set|whyImageMediaId"),
                SocialImageMediaId = MediaId(form, "set|socialImageMediaId"),
            }
            : new CopyOverrideSettings();
        return new CopyOverride { Translations = translations, Settings = settings };
    }

    // ---------------------------------------------------------------------------------------------- Settings ---

    public static SiteSettingsContent BuildSettings(IReadOnlyDictionary<string, StringValues> form, IReadOnlyList<string> languages, int phoneRows, int officeRows, int socialRows)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(languages);
        Dictionary<string, string> Localized(string name) =>
            languages.Select(language => (language, value: Value(form, Field(name, language)).Trim()))
                .Where(entry => entry.value.Length > 0)
                .ToDictionary(entry => entry.language, entry => entry.value, StringComparer.Ordinal);

        var phones = Enumerable.Range(0, phoneRows)
            .Select(row => new SitePhone { Number = Value(form, Field("phone", row.ToString(System.Globalization.CultureInfo.InvariantCulture), "number")).Trim(), Label = Localized(Field("phone", row.ToString(System.Globalization.CultureInfo.InvariantCulture), "label")) })
            .Where(phone => phone.Number.Length > 0)
            .ToList();
        var offices = Enumerable.Range(0, officeRows)
            .Select(row => row.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Select(row => new SiteOffice { Name = Localized(Field("office", row, "name")), Address = Localized(Field("office", row, "address")) })
            .Where(office => office.Name.Count > 0 || office.Address.Count > 0)
            .ToList();
        var social = Enumerable.Range(0, socialRows)
            .Select(row => row.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Select(row => new SiteSocialLink { Network = Value(form, Field("social", row, "network")).Trim(), Url = Value(form, Field("social", row, "url")).Trim() })
            .Where(link => link.Url.Length > 0)
            .ToList();
        return new SiteSettingsContent
        {
            CompanyName = Localized("companyName"),
            ContactEmail = Blank(Value(form, "contactEmail")),
            Phones = phones,
            Offices = offices,
            SocialLinks = social,
        };
    }

    // ------------------------------------------------------------------------------------------------ Helpers ---

    /// <summary>Splits a textarea into list entries: one per line, or paragraphs separated by a blank line.</summary>
    public static IReadOnlyList<string> Lines(string? value, bool paragraphs = false)
    {
        var normalized = (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var parts = paragraphs ? normalized.Split("\n\n") : normalized.Split('\n');
        return parts.Select(part => part.Replace('\n', ' ').Trim()).Where(part => part.Length > 0).ToList();
    }

    public static string JoinLines(IEnumerable<string> values, bool paragraphs = false) => string.Join(paragraphs ? "\n\n" : "\n", values);

    private static string Value(IReadOnlyDictionary<string, StringValues> form, string name) =>
        form.TryGetValue(name, out var values) ? values.ToString() : string.Empty;

    private static IReadOnlyList<string> Values(IReadOnlyDictionary<string, StringValues> form, string name) =>
        form.TryGetValue(name, out var values) ? values.Where(value => value is not null).Select(value => value!).ToList() : [];

    private static string? Changed(IReadOnlyDictionary<string, StringValues> form, string name, string? fallback) => Changed(Value(form, name), fallback);

    private static string? Changed(string submitted, string? fallback)
    {
        var value = submitted.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return value.Length == 0 || string.Equals(value, (fallback ?? string.Empty).Trim(), StringComparison.Ordinal) ? null : value;
    }

    private static List<string>? ChangedList(IReadOnlyDictionary<string, StringValues> form, string name, IReadOnlyList<string> fallback, bool paragraphs = false)
    {
        if (!form.ContainsKey(name))
        {
            return null;
        }

        var values = Lines(Value(form, name), paragraphs);
        return values.Count == 0 || values.SequenceEqual(fallback.Select(entry => entry.Trim()), StringComparer.Ordinal) ? null : values.ToList();
    }

    private static List<string> Order(IReadOnlyDictionary<string, StringValues> form, string prefix, IReadOnlyList<string> keys)
    {
        var positions = keys
            .Select((key, index) => (key, index, position: int.TryParse(Value(form, prefix + key), out var number) ? number : index + 1))
            .OrderBy(entry => entry.position)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.key)
            .ToList();
        return positions.SequenceEqual(keys, StringComparer.Ordinal) ? [] : positions;
    }

    private static string? MediaId(IReadOnlyDictionary<string, StringValues> form, string name) =>
        Guid.TryParse(Value(form, name), out var id) ? id.ToString("D") : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmpty(ItemOverride item) =>
        item.Title is null && item.Text is null && item.Points is null && item.Tags is null && item.Deliverables is null && item.Involvement is null;

    private static bool IsEmpty(SectionOverride section) =>
        section.Eyebrow is null && section.Heading is null && section.Lead is null && section.Note is null && section.Paragraphs is null && section.Items.Count == 0;
}
