using System.Text.Json;
using System.Text.Json.Serialization;
using Onyx.Application.Abstractions;
using Onyx.Application.Auditing;
using Onyx.Domain.Auditing;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Auditing;

public sealed class AuditTrail(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IRequestContext requestContext,
    TimeProvider timeProvider) : IAuditTrail
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Record(AuditAction action, AuditCategory category, string entityType, string? entityId, object? details = null, Guid? actorUserId = null)
    {
        dbContext.AuditLogs.Add(AuditLog.Record(
            timeProvider.GetUtcNow(),
            action,
            category,
            entityType,
            entityId,
            details is null ? null : JsonSerializer.Serialize(details, SerializerOptions),
            actorUserId ?? currentUser.UserId,
            currentUser.UserName,
            requestContext.IpAddress,
            requestContext.CorrelationId));
    }
}
