using Onyx.Domain.Common;

namespace Onyx.Application.Email;

public interface IEmailTemplateRenderer
{
    /// <summary>Fails (rather than falling back to another language) when the template or language is unknown.</summary>
    /// <summary>Uses the admin-edited wording when one exists for the template and language, otherwise the code default.</summary>
    Task<Result<RenderedEmail>> RenderAsync(string templateKey, string languageCode, IReadOnlyDictionary<string, string> model, CancellationToken cancellationToken);
}
