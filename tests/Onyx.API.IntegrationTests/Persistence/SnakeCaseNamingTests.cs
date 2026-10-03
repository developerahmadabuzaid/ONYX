using Onyx.Infrastructure.Persistence.Conventions;

namespace Onyx.API.IntegrationTests.Persistence;

public sealed class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("AuditLogs", "audit_logs")]
    [InlineData("CreatedAtUtc", "created_at_utc")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("UserId", "user_id")]
    [InlineData("Utf8Name", "utf8_name")]
    [InlineData("PK_AuditLogs", "pk_audit_logs")]
    [InlineData("IX_AuditLogs_EntityType_EntityId", "ix_audit_logs_entity_type_entity_id")]
    [InlineData("already_snake", "already_snake")]
    public void Converts_identifiers(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }
}
