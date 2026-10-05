using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

/// <summary>
/// Retention policies and their enforcement (architecture.md §14, D20). No period is preset: a category is processed only when
/// an administrator has saved an enabled policy with a period, so a fresh installation never deletes or anonymizes anything.
/// Enforcement works in bounded batches, each in its own transaction, so a crash or redelivery simply continues where it
/// stopped (already processed records no longer match). Each category run is audited with counts only.
/// </summary>
public sealed class RetentionService(
    IRetentionPolicyRepository policies,
    IPersonalDataStore store,
    SubjectEraser eraser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    RetentionSettings settings,
    TimeProvider timeProvider) : IRetentionService
{
    public async Task<IReadOnlyList<RetentionPolicyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var saved = (await policies.ListAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(policy => policy.Category);
        return Enum.GetValues<RetentionCategory>()
            .Select(category => saved.TryGetValue(category, out var policy) ? ToResponse(policy) : Unsaved(category))
            .ToList();
    }

    public async Task<Result<RetentionPolicyResponse>> SaveAsync(string category, SaveRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<RetentionCategory>(category, out var parsedCategory))
        {
            return Error.NotFound("Retention.UnknownCategory", "Unknown retention category.");
        }

        if (!EnumParsing.TryParse<RetentionAction>(request.Action, out var action))
        {
            return Error.Validation("Retention.InvalidAction", "The action is Delete or Anonymize.");
        }

        var policy = await policies.GetAsync(parsedCategory, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            var created = RetentionPolicy.Create(parsedCategory, action, request.PeriodDays, request.IsEnabled);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            policy = created.Value;
            policies.Add(policy);
        }
        else
        {
            if (request.Version is { } version)
            {
                policies.SetExpectedVersion(policy, version);
            }

            var updated = policy.Update(action, request.PeriodDays, request.IsEnabled);
            if (updated.IsFailure)
            {
                return updated.Error!;
            }
        }

        auditTrail.Record(
            AuditAction.RetentionPolicyChanged, AuditCategory.Privacy, nameof(RetentionPolicy), parsedCategory.ToString(),
            new { action = policy.Action.ToString(), periodDays = policy.PeriodDays, enabled = policy.IsEnabled });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(policy);
    }

    public async Task<Result<RetentionPreviewResponse>> PreviewAsync(string category, string action, int periodDays, CancellationToken cancellationToken)
    {
        if (!EnumParsing.TryParse<RetentionCategory>(category, out var parsedCategory))
        {
            return Error.NotFound("Retention.UnknownCategory", "Unknown retention category.");
        }

        if (!EnumParsing.TryParse<RetentionAction>(action, out var parsedAction) || !RetentionPolicy.AllowedActions(parsedCategory).Contains(parsedAction))
        {
            return Error.Validation("Retention.ActionNotAllowed", "This action is not available for the category.");
        }

        if (periodDays is < 1 or > RetentionPolicy.MaxPeriodDays)
        {
            return Error.Validation("Retention.InvalidPeriod", $"The period is 1 to {RetentionPolicy.MaxPeriodDays} days.");
        }

        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-periodDays);
        var count = parsedCategory == RetentionCategory.LeadsAndInquiries
            ? await store.CountLeadsDueAsync(cutoff, now, parsedAction == RetentionAction.Anonymize, cancellationToken).ConfigureAwait(false)
            : await store.CountRetentionDueAsync(parsedCategory, parsedAction, cutoff, cancellationToken).ConfigureAwait(false);
        return new RetentionPreviewResponse(parsedCategory.ToString(), parsedAction.ToString(), periodDays, cutoff, count);
    }

    public async Task<IReadOnlyList<RetentionRunSummary>> EnforceAsync(CancellationToken cancellationToken)
    {
        var active = (await policies.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(policy => policy.IsActive)
            .OrderBy(policy => policy.Category)
            .Select(policy => (policy.Category, policy.Action, policy.PeriodDays!.Value))
            .ToList();

        var summaries = new List<RetentionRunSummary>();
        foreach (var (category, action, periodDays) in active)
        {
            var now = timeProvider.GetUtcNow();
            var cutoff = now.AddDays(-periodDays);
            var processed = 0;
            var more = false;
            for (var batch = 0; batch < settings.MaxBatchesPerRun; batch++)
            {
                var count = await RunBatchAsync(category, action, cutoff, now, cancellationToken).ConfigureAwait(false);
                processed += count;
                if (count < settings.BatchSize)
                {
                    break;
                }

                more = batch == settings.MaxBatchesPerRun - 1;
            }

            auditTrail.Record(
                AuditAction.RetentionRunCompleted, AuditCategory.Privacy, nameof(RetentionPolicy), category.ToString(),
                new { action = action.ToString(), periodDays, cutoff, processed, moreRemaining = more });
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            summaries.Add(new RetentionRunSummary(category.ToString(), action.ToString(), processed, more));
        }

        return summaries;
    }

    private Task<int> RunBatchAsync(RetentionCategory category, RetentionAction action, DateTimeOffset cutoff, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (category != RetentionCategory.LeadsAndInquiries)
        {
            return store.ApplyRetentionBatchAsync(category, action, cutoff, settings.BatchSize, cancellationToken);
        }

        return ApplyLeadsBatchAsync(action, cutoff, now, cancellationToken);
    }

    private async Task<int> ApplyLeadsBatchAsync(RetentionAction action, DateTimeOffset cutoff, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync<int>(async token =>
        {
            var leadIds = await store.ListLeadsDueAsync(cutoff, now, excludeErased: action == RetentionAction.Anonymize, settings.BatchSize, token).ConfigureAwait(false);
            if (leadIds.Count == 0)
            {
                return Result<int>.Success(0);
            }

            if (action == RetentionAction.Delete)
            {
                await store.DeleteLeadsAsync(leadIds, token).ConfigureAwait(false);
            }
            else
            {
                var records = await store.LoadLeadsAsync(leadIds, token).ConfigureAwait(false);
                await eraser.EraseAsync(records, null, now, token).ConfigureAwait(false);
                await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            }

            return Result<int>.Success(leadIds.Count);
        }, cancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    private static RetentionPolicyResponse ToResponse(RetentionPolicy policy) =>
        new(
            policy.Category.ToString(), policy.Action.ToString(), policy.PeriodDays, policy.IsEnabled, policy.IsActive, true,
            RetentionPolicy.AllowedActions(policy.Category).Select(action => action.ToString()).Order(StringComparer.Ordinal).ToList(), policy.Version);

    private static RetentionPolicyResponse Unsaved(RetentionCategory category) =>
        new(
            category.ToString(), RetentionAction.Delete.ToString(), null, false, false, false,
            RetentionPolicy.AllowedActions(category).Select(action => action.ToString()).Order(StringComparer.Ordinal).ToList(), null);
}
