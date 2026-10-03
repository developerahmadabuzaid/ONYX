using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;

namespace Onyx.Application.Auditing;

public interface IAuditLogQueries
{
    public const int MaxPageSize = 100;

    Task<PagedResult<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
