using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Contracts.Knowledge;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Areas.Admin.Models;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Approved company knowledge: the only facts AI-assisted drafting may rely on. Approval requires a recorded source. Missing facts are
/// raised as information requests instead of being guessed.
/// </summary>
[Route("admin/knowledge")]
public sealed class KnowledgeController(IAdminCmsApiClient cms) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        var entries = await cms.ListKnowledgeAsync(Blank(status), cancellationToken);
        var requests = await cms.ListInformationRequestsAsync(cancellationToken);
        var rules = await cms.GetGovernanceRulesAsync(cancellationToken);
        return Failed(entries) ?? Failed(requests) ?? Failed(rules) ?? View(new KnowledgeListViewModel(entries.Value!, requests.Value!, rules.Value!, status));
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(string category, string title, string? statementAr, string? statementEn, string? sourceNote, CancellationToken cancellationToken)
    {
        var result = await cms.CreateKnowledgeAsync(new SaveKnowledgeEntryRequest(category, title ?? string.Empty, Statements(statementAr, statementEn), Blank(sourceNote), null), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, string category, string title, string? statementAr, string? statementEn, string? sourceNote, uint version, CancellationToken cancellationToken)
    {
        var result = await cms.UpdateKnowledgeAsync(id, new SaveKnowledgeEntryRequest(category, title ?? string.Empty, Statements(statementAr, statementEn), Blank(sourceNote), version), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)));
    }

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, string operation, uint version, CancellationToken cancellationToken)
    {
        if (operation is not ("approve" or "retire"))
        {
            return ShowError("Cms.InvalidAction", RedirectToAction(nameof(Index)));
        }

        var result = await cms.KnowledgeActionAsync(id, operation, version, cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Done");
    }

    [HttpPost("information-requests")]
    public async Task<IActionResult> Raise(string question, string? context, Guid? contentItemId, CancellationToken cancellationToken)
    {
        var result = await cms.RaiseInformationRequestAsync(new RaiseInformationRequest(question ?? string.Empty, Blank(context), contentItemId), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("information-requests/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, string outcome, Guid? knowledgeEntryId, string? note, CancellationToken cancellationToken)
    {
        var result = await cms.ResolveInformationRequestAsync(id, new ResolveInformationRequest(outcome, knowledgeEntryId, Blank(note)), cancellationToken);
        return After(result, RedirectToAction(nameof(Index)), "Cms.Done");
    }

    private static List<LocalizedTextDto> Statements(string? arabic, string? english)
    {
        var statements = new List<LocalizedTextDto>();
        if (Blank(arabic) is { } ar)
        {
            statements.Add(new LocalizedTextDto("ar", ar));
        }

        if (Blank(english) is { } en)
        {
            statements.Add(new LocalizedTextDto("en", en));
        }

        return statements;
    }
}
