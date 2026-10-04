using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Common;
using Onyx.Application.Localization;
using Onyx.Contracts.Content;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public sealed class CallToActionService(ICallToActionRepository ctas, IUnitOfWork unitOfWork) : ICallToActionService
{
    public async Task<IReadOnlyList<CtaResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await ctas.ListAsync(cancellationToken).ConfigureAwait(false)).Select(ToResponse).ToList();

    public async Task<Result<CtaResponse>> CreateAsync(SaveCtaRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        if (await ctas.KeyExistsAsync(request.Key.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Cta.KeyTaken", "Another call to action uses this key.");
        }

        var created = CallToAction.Create(request.Key, parsed.Value, request.TargetUrl, ContentMapping.ToDomain(request.Texts));
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (!request.IsActive)
        {
            created.Value.Update(parsed.Value, request.TargetUrl, ContentMapping.ToDomain(request.Texts), isActive: false);
        }

        ctas.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<CtaResponse>> UpdateAsync(Guid id, SaveCtaRequest request, CancellationToken cancellationToken)
    {
        var parsed = Parse(request);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var cta = await ctas.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (cta is null)
        {
            return Error.NotFound("Cta.NotFound", "Call to action not found.");
        }

        if (request.Version is { } version)
        {
            ctas.SetExpectedVersion(cta, version);
        }

        var updated = cta.Update(parsed.Value, request.TargetUrl, ContentMapping.ToDomain(request.Texts), request.IsActive);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(cta);
    }

    public async Task<CtaDefaultsResponse> GetDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = await ctas.ListDefaultsAsync(cancellationToken).ConfigureAwait(false);
        return new CtaDefaultsResponse(
            defaults.FirstOrDefault(entry => entry.ContentType is null)?.CallToActionId,
            defaults.Where(entry => entry.ContentType is not null).ToDictionary(entry => entry.ContentType!.Value.ToString(), entry => entry.CallToActionId));
    }

    public async Task<Result<CtaDefaultsResponse>> SetDefaultAsync(SetCtaDefaultRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ContentType? type = null;
        if (request.ContentType is not null)
        {
            if (!EnumParsing.TryParse<ContentType>(request.ContentType, out var parsedType))
            {
                return Error.Validation("Content.InvalidType", "Unknown content type.");
            }

            type = parsedType;
        }

        if (request.CallToActionId is { } ctaId && await ctas.GetAsync(ctaId, cancellationToken).ConfigureAwait(false) is not { IsActive: true })
        {
            return Error.Validation("Cta.NotFound", "Choose an active call to action.");
        }

        var defaults = await ctas.ListDefaultsAsync(cancellationToken).ConfigureAwait(false);
        var existing = defaults.FirstOrDefault(entry => entry.ContentType == type);
        if (request.CallToActionId is null)
        {
            if (existing is not null)
            {
                ctas.RemoveDefault(existing);
            }
        }
        else if (existing is null)
        {
            ctas.AddDefault(CtaDefault.Create(type, request.CallToActionId.Value));
        }
        else
        {
            existing.Change(request.CallToActionId.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetDefaultsAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result<CtaActionType> Parse(SaveCtaRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Key) || !EnumParsing.TryParse<CtaActionType>(request.ActionType, out var action))
        {
            return Error.Validation("Cta.Invalid", "A key and a known action type are required.");
        }

        return (request.Texts ?? []).Any(text => !SupportedLanguages.All.Contains(text.LanguageCode))
            ? Error.Validation("Cta.UnsupportedLanguage", "Labels must use configured languages.")
            : action;
    }

    private static CtaResponse ToResponse(CallToAction cta) =>
        new(cta.Id, cta.Key, cta.ActionType.ToString(), cta.TargetUrl, cta.Texts.Select(ContentMapping.ToDto).ToList(), cta.IsActive, cta.Version);
}
