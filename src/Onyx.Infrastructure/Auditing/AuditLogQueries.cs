using Microsoft.EntityFrameworkCore;
using Onyx.Application.Auditing;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Auditing;

public sealed class AuditLogQueries(ApplicationDbContext dbContext) : IAuditLogQueries
{
    public async Task<PagedResult<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, IAuditLogQueries.MaxPageSize);

        var logs = dbContext.AuditLogs.AsNoTracking();
        if (query.UserId is { } userId)
        {
            logs = logs.Where(log => log.UserId == userId);
        }

        if (Enum.TryParse<AuditCategory>(query.Category, ignoreCase: true, out var category))
        {
            logs = logs.Where(log => log.Category == category);
        }

        if (Enum.TryParse<AuditAction>(query.Action, ignoreCase: true, out var action))
        {
            logs = logs.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(log => log.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            logs = logs.Where(log => log.EntityId == query.EntityId);
        }

        if (query.FromUtc is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            logs = logs.Where(log => log.OccurredAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } to)
        {
            var toUtc = to.ToUniversalTime();
            logs = logs.Where(log => log.OccurredAtUtc <= toUtc);
        }

        var total = await logs.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(log => new AuditLogResponse(
            log.Id, log.OccurredAtUtc, log.Action.ToString(), log.Category.ToString(), log.EntityType, log.EntityId,
            log.Changes, log.UserId, log.IpAddress, log.CorrelationId)).ToList();

        return new PagedResult<AuditLogResponse>(items, page, pageSize, total);
    }
}
