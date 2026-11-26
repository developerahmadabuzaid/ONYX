using Onyx.Application.Abstractions.Persistence;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>Saved issue searches. Each person sees and removes only their own; someone else's filter does not exist for them.</summary>
public sealed class SavedIssueFilterService(
    ISavedIssueFilterRepository filters,
    IDeliveryReadModel readModel,
    DeliveryAccess access,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly Error NotFound = Error.NotFound("Delivery.FilterNotFound", "Saved filter not found.");

    public async Task<IReadOnlyList<SavedIssueFilterResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        return await readModel.SavedFiltersAsync(viewer.UserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<SavedIssueFilterResponse>> CreateAsync(SaveIssueFilterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        if (await filters.CountForOwnerAsync(viewer.UserId, cancellationToken).ConfigureAwait(false) >= SavedIssueFilter.MaxPerUser)
        {
            return Error.Validation("Delivery.TooManyFilters", $"You can keep up to {SavedIssueFilter.MaxPerUser} saved filters. Remove one first.");
        }

        var created = SavedIssueFilter.Create(viewer.UserId, request.Name, request.Query, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error!;
        }

        filters.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SavedIssueFilterResponse(created.Value.Id, created.Value.Name, created.Value.Query, created.Value.CreatedAtUtc);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var filter = await filters.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (filter is null || filter.OwnerId != viewer.UserId)
        {
            return NotFound;
        }

        filters.Remove(filter);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
