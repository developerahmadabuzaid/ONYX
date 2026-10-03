using Onyx.Domain.Common;

namespace Onyx.Application.Email;

public interface IEmailTemplateRenderer
{
    /// <summary>Fails (rather than falling back to another language) when the template or language is unknown.</summary>
    Result<RenderedEmail> Render(string templateKey, string languageCode, IReadOnlyDictionary<string, string> model);
}
