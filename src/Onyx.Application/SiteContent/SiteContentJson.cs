using System.Text.Json;

namespace Onyx.Application.SiteContent;

/// <summary>
/// Structural checks for a site content document: a JSON object of bounded depth whose text values carry no markup or control
/// characters (they are rendered as encoded plain text, never as HTML), and the media assets it references.
/// </summary>
internal static class SiteContentJson
{
    public const int MaxDepth = 12;
    public const int MaxStringLength = 4_000;

    private static readonly JsonDocumentOptions Options = new() { MaxDepth = MaxDepth + 2, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false };

    /// <summary>Null when valid; otherwise the reason. <paramref name="mediaIds"/> receives every *MediaId value.</summary>
    public static string? Validate(string json, out IReadOnlyList<Guid> mediaIds)
    {
        mediaIds = [];
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, Options);
        }
        catch (JsonException)
        {
            return "The content is not valid JSON.";
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "The content must be a JSON object.";
            }

            var found = new List<Guid>();
            var problem = Walk(document.RootElement, null, 0, found);
            mediaIds = found.Distinct().ToList();
            return problem;
        }
    }

    private static string? Walk(JsonElement element, string? propertyName, int depth, List<Guid> mediaIds)
    {
        if (depth > MaxDepth)
        {
            return "The content is nested too deeply.";
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Length > 120)
                    {
                        return "A field name is too long.";
                    }

                    var problem = Walk(property.Value, property.Name, depth + 1, mediaIds);
                    if (problem is not null)
                    {
                        return problem;
                    }
                }

                return null;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var problem = Walk(item, propertyName, depth + 1, mediaIds);
                    if (problem is not null)
                    {
                        return problem;
                    }
                }

                return null;
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                if (propertyName is not null && propertyName.EndsWith("MediaId", StringComparison.Ordinal))
                {
                    if (value.Length == 0)
                    {
                        return null;
                    }

                    if (!Guid.TryParse(value, out var mediaId))
                    {
                        return $"'{propertyName}' must be a media id.";
                    }

                    mediaIds.Add(mediaId);
                    return null;
                }

                if (value.Length > MaxStringLength)
                {
                    return $"A text value is longer than {MaxStringLength} characters.";
                }

                if (value.Any(character => char.IsControl(character) && character is not ('\n' or '\r' or '\t')))
                {
                    return "Text values may not contain control characters.";
                }

                // Plain text only: the public site encodes everything, and markup in copy is never intended.
                if (value.Contains('<', StringComparison.Ordinal) || value.Contains('>', StringComparison.Ordinal))
                {
                    return "Text values may not contain HTML or angle brackets.";
                }

                return null;
            default:
                return null;
        }
    }
}
