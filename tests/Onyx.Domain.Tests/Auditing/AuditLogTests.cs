using Onyx.Domain.Auditing;

namespace Onyx.Domain.Tests.Auditing;

public sealed class AuditLogTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Record_captures_all_fields()
    {
        var userId = Guid.NewGuid();

        var log = AuditLog.Record(UtcNow, AuditAction.Updated, AuditCategory.Security, "User", "42", "{}", userId, "admin", "10.0.0.1", "corr-12345");

        Assert.Equal(UtcNow, log.OccurredAtUtc);
        Assert.Equal(AuditAction.Updated, log.Action);
        Assert.Equal(AuditCategory.Security, log.Category);
        Assert.Equal("User", log.EntityType);
        Assert.Equal("42", log.EntityId);
        Assert.Equal(userId, log.UserId);
        Assert.Equal("corr-12345", log.CorrelationId);
    }

    [Fact]
    public void Non_utc_timestamps_are_rejected()
    {
        var riyadhTime = new DateTimeOffset(2026, 10, 3, 15, 0, 0, TimeSpan.FromHours(3));

        Assert.Throws<ArgumentException>(() =>
            AuditLog.Record(riyadhTime, AuditAction.Created, AuditCategory.Content, "Service", null, null, null, null, null, null));
    }

    [Fact]
    public void Overlong_values_are_truncated_to_column_limits()
    {
        var longName = new string('x', 1_000);

        var log = AuditLog.Record(UtcNow, AuditAction.Created, AuditCategory.Content, longName, longName, null, null, longName, longName, longName);

        Assert.Equal(AuditLog.EntityTypeMaxLength, log.EntityType.Length);
        Assert.Equal(AuditLog.EntityIdMaxLength, log.EntityId!.Length);
        Assert.Equal(AuditLog.UserNameMaxLength, log.UserName!.Length);
        Assert.Equal(AuditLog.IpAddressMaxLength, log.IpAddress!.Length);
        Assert.Equal(AuditLog.CorrelationIdMaxLength, log.CorrelationId!.Length);
    }
}
