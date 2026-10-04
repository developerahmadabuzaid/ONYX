using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Email;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Messaging;

namespace Onyx.API.Controllers.Admin;

/// <summary>Admin-editable email wording (plain text + declared placeholders). Links and layout stay code-owned.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/email-templates")]
public sealed class AdminEmailTemplatesController(IEmailTemplateAdministrationService templates) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.EmailTemplates.Manage)]
    [ProducesResponseType<IReadOnlyList<EmailTemplateResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmailTemplateResponse>>> List(CancellationToken cancellationToken) => Ok(await templates.ListAsync(cancellationToken));

    [HttpPut("{templateKey}/{languageCode:length(2)}")]
    [HasPermission(Permissions.EmailTemplates.Manage)]
    [ProducesResponseType<EmailTemplateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Save(string templateKey, string languageCode, SaveEmailTemplateCopyRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await templates.SaveCopyAsync(templateKey, languageCode, request, cancellationToken));

    [HttpDelete("{templateKey}/{languageCode:length(2)}")]
    [HasPermission(Permissions.EmailTemplates.Manage)]
    [ProducesResponseType<EmailTemplateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reset(string templateKey, string languageCode, CancellationToken cancellationToken) =>
        this.OkOrProblem(await templates.ResetCopyAsync(templateKey, languageCode, cancellationToken));
}
