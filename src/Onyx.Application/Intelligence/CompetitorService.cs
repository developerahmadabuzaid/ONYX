using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Localization;
using Onyx.Contracts.Intelligence;
using Onyx.Domain.Common;
using Onyx.Domain.Intelligence;

namespace Onyx.Application.Intelligence;

/// <summary>Internal-only competitor references. No crawling or scraping happens here.</summary>
public sealed class CompetitorService(ICompetitorRepository competitors, IUnitOfWork unitOfWork) : ICompetitorService
{
    private static readonly Error NotFound = Error.NotFound("Competitor.NotFound", "Competitor not found.");

    public async Task<IReadOnlyList<CompetitorResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await competitors.ListAsync(cancellationToken).ConfigureAwait(false)).Select(ToResponse).ToList();

    public async Task<Result<CompetitorResponse>> CreateAsync(SaveCompetitorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = CompetitorReference.Create(request.Name, request.Domain);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (await competitors.DomainExistsAsync(created.Value.Domain, null, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Competitor.Exists", "This domain is already listed.");
        }

        var updated = Apply(created.Value, request);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        competitors.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<CompetitorResponse>> UpdateAsync(Guid id, SaveCompetitorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reference = await competitors.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (reference is null)
        {
            return NotFound;
        }

        if (!string.Equals(reference.Domain, request.Domain?.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return Error.Validation("Competitor.DomainImmutable", "The domain cannot change; add a new reference instead.");
        }

        if (request.Version is { } version)
        {
            competitors.SetExpectedVersion(reference, version);
        }

        var updated = Apply(reference, request);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(reference);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var reference = await competitors.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (reference is null)
        {
            return NotFound;
        }

        competitors.Remove(reference);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static Result Apply(CompetitorReference reference, SaveCompetitorRequest request)
    {
        var languages = request.Languages ?? [];
        if (languages.Any(language => !SupportedLanguages.All.Contains(language)))
        {
            return Error.Validation("Competitor.UnsupportedLanguage", "Languages must be configured site languages.");
        }

        return reference.Update(request.Name, (request.Markets ?? []).Select(market => market.Trim().ToUpperInvariant()).ToList(), languages, request.Topics ?? [],
            request.Notes, request.IsActive);
    }

    private static CompetitorResponse ToResponse(CompetitorReference reference) =>
        new(reference.Id, reference.Name, reference.Domain, reference.Markets, reference.Languages, reference.Topics, reference.Notes, reference.IsActive, reference.Version);
}
