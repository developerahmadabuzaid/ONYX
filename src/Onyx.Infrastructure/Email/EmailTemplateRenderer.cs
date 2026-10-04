using System.Text;
using System.Text.Encodings.Web;
using Onyx.Application.Email;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.Infrastructure.Email;

/// <summary>
/// Wraps template content in the shared, neutral email layout (visual email design is deferred with the design stage).
/// Every dynamic value is HTML-encoded; direction and alignment follow the language (RTL for Arabic).
/// </summary>
public sealed class EmailTemplateRenderer(IEnumerable<IEmailTemplate> templates, IEmailTemplateCopyRepository copies) : IEmailTemplateRenderer
{
    private const string BrandName = "Onyx Vanguard Solutions";
    private static readonly HashSet<string> RightToLeftLanguages = new(StringComparer.Ordinal) { "ar" };

    private readonly Dictionary<string, IEmailTemplate> _templates = templates.ToDictionary(template => template.Key, StringComparer.Ordinal);

    public async Task<Result<RenderedEmail>> RenderAsync(
        string templateKey, string languageCode, IReadOnlyDictionary<string, string> model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!_templates.TryGetValue(templateKey, out var template))
        {
            return Error.Validation("Email.UnknownTemplate", $"Email template '{templateKey}' does not exist.");
        }

        if (!template.Languages.Contains(languageCode))
        {
            return Error.Validation("Email.UnsupportedLanguage", $"Email template '{templateKey}' has no '{languageCode}' version.");
        }

        var missing = template.RequiredValues.Where(key => !model.ContainsKey(key)).ToList();
        if (missing.Count > 0)
        {
            return Error.Validation("Email.MissingValues", $"Missing template values: {string.Join(", ", missing)}.");
        }

        // Admin-edited wording replaces only the text; the action URL still comes from the template's code.
        var content = template is IEditableEmailTemplate editable
            && await copies.FindAsync(templateKey, languageCode, cancellationToken).ConfigureAwait(false) is { } custom
            ? EditableEmail.Compose(editable, new EmailCopy(custom.Subject, custom.Heading, custom.Paragraphs, custom.ActionLabel, custom.Footer), model)
            : template.Compose(languageCode, model);
        if (content.Action is { } action && !IsAllowedLink(action.Url))
        {
            return Error.Validation("Email.UnsafeLink", "Email links must be absolute https URLs.");
        }

        return new RenderedEmail(content.Subject, RenderHtml(content, languageCode), RenderText(content));
    }

    private static bool IsAllowedLink(Uri url) =>
        url.IsAbsoluteUri && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback));

    private static string RenderHtml(EmailContent content, string languageCode)
    {
        var encoder = HtmlEncoder.Default;
        var rtl = RightToLeftLanguages.Contains(languageCode);
        var direction = rtl ? "rtl" : "ltr";
        var align = rtl ? "right" : "left";
        var html = new StringBuilder(2_048);

        html.Append("<!DOCTYPE html><html lang=\"").Append(encoder.Encode(languageCode)).Append("\" dir=\"").Append(direction).Append("\">")
            .Append("<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<title>").Append(encoder.Encode(content.Subject)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;background-color:#ffffff;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td align=\"center\" style=\"padding:24px 12px;\">")
            .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"").Append(direction)
            .Append("\" style=\"max-width:600px;width:100%;font-family:Arial,Helvetica,sans-serif;color:#111111;text-align:").Append(align).Append(";\">")
            .Append("<tr><td style=\"padding:0 0 24px;font-size:14px;font-weight:bold;letter-spacing:0.5px;\">").Append(encoder.Encode(BrandName)).Append("</td></tr>")
            .Append("<tr><td style=\"padding:0 0 16px;font-size:20px;line-height:1.4;font-weight:bold;\">").Append(encoder.Encode(content.Heading)).Append("</td></tr>");

        foreach (var paragraph in content.Paragraphs)
        {
            html.Append("<tr><td style=\"padding:0 0 12px;font-size:15px;line-height:1.7;\">").Append(encoder.Encode(paragraph)).Append("</td></tr>");
        }

        if (content.Action is { } action)
        {
            html.Append("<tr><td style=\"padding:12px 0 20px;\"><a href=\"").Append(encoder.Encode(action.Url.AbsoluteUri))
                .Append("\" style=\"display:inline-block;padding:12px 20px;background-color:#111111;color:#ffffff;text-decoration:none;font-size:15px;\">")
                .Append(encoder.Encode(action.Label)).Append("</a></td></tr>");
        }

        if (!string.IsNullOrWhiteSpace(content.Footer))
        {
            html.Append("<tr><td style=\"padding:24px 0 0;border-top:1px solid #e5e5e5;font-size:12px;line-height:1.6;color:#555555;\">")
                .Append(encoder.Encode(content.Footer)).Append("</td></tr>");
        }

        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static string RenderText(EmailContent content)
    {
        var text = new StringBuilder(1_024);
        text.AppendLine(BrandName).AppendLine().AppendLine(content.Heading).AppendLine();
        foreach (var paragraph in content.Paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        if (content.Action is { } action)
        {
            text.Append(action.Label).Append(": ").AppendLine(action.Url.AbsoluteUri).AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(content.Footer))
        {
            text.AppendLine("—").AppendLine(content.Footer);
        }

        return text.ToString();
    }
}
