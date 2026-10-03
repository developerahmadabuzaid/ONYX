using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Identity;
using Onyx.Application.Notifications;
using Onyx.Application.Providers;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Auditing;
using Onyx.Domain.Notifications;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.Email.Templates;

namespace Onyx.API.IntegrationTests.Identity;

/// <summary>The Stage 2 features exposed through the now-protected admin API: notifications, provider connections, audit logs.</summary>
public sealed class AdminOperationsApiTests(PostgresFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Notification_api_shows_each_user_only_what_their_permissions_allow()
    {
        Fixture.RequireDatabase();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        using var sales = AuthorizedClient((await SignInAsync("sales@onyx.test", StrongPassword)).AccessToken);
        var leadNotification = await PublishAsync(NotificationType.NewLead, Permissions.Leads.View);
        var jobNotification = await PublishAsync(NotificationType.JobFailed, Permissions.SystemJobs.View);

        var list = await sales.GetFromJsonAsync<PagedResult<AdminNotificationResponse>>("/api/v1/admin/notifications", Token);
        Assert.Equal(leadNotification, Assert.Single(list!.Items).Id);
        Assert.Equal(1, await sales.GetFromJsonAsync<int>("/api/v1/admin/notifications/unread-count", Token));

        using var hidden = await sales.PostAsync($"/api/v1/admin/notifications/{jobNotification}/read", null, Token);
        using var visible = await sales.PostAsync($"/api/v1/admin/notifications/{leadNotification}/read", null, Token);

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, visible.StatusCode);
        Assert.Equal(0, await sales.GetFromJsonAsync<int>("/api/v1/admin/notifications/unread-count", Token));
    }

    [Fact]
    public async Task Provider_connections_can_be_listed_disabled_enabled_and_tested_with_audit()
    {
        Fixture.RequireDatabase();
        var (ownerId, owner) = await SignInSuperAdminAsync();
        await InScopeAsync(async services =>
        {
            await services.GetRequiredService<IProviderConnectionService>().ReportNotConfiguredAsync("email.smtp", ProviderCategory.Email, Token);
            return 0;
        });

        var list = await owner.GetFromJsonAsync<List<ProviderConnectionResponse>>("/api/v1/admin/provider-connections", Token);
        using var disable = await owner.PostAsync("/api/v1/admin/provider-connections/email.smtp/disable", null, Token);
        var disabled = await disable.Content.ReadFromJsonAsync<ProviderConnectionResponse>(Token);
        using var enable = await owner.PostAsync("/api/v1/admin/provider-connections/email.smtp/enable", null, Token);
        using var missing = await owner.PostAsync("/api/v1/admin/provider-connections/unknown.provider/disable", null, Token);
        using var test = await owner.PostAsync("/api/v1/admin/provider-connections/email/test", null, Token);

        Assert.Equal("NotConfigured", Assert.Single(list!).Status);
        Assert.Equal("Disabled", disabled!.Status);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, test.StatusCode);

        var email = Assert.Single(await QueryAsync(db => db.EmailMessages));
        Assert.Equal(ProviderTestEmailTemplate.TemplateKey, email.TemplateKey);
        Assert.Equal("owner@onyx.test", email.ToAddress);

        var audits = await QueryAsync(db => db.AuditLogs.Where(log => log.Category == AuditCategory.Integrations));
        Assert.Contains(audits, log => log.Action == AuditAction.ProviderConnectionDisabled && log.UserId == ownerId);
        Assert.Contains(audits, log => log.Action == AuditAction.ProviderConnectionEnabled);
        Assert.Contains(audits, log => log.Action == AuditAction.ProviderTestRequested);
        owner.Dispose();
    }

    [Fact]
    public async Task Audit_log_api_filters_security_events_and_never_contains_secrets()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await CreateUserAsync("sales@onyx.test", StrongPassword, SystemRoles.Sales);
        var salesTokens = await SignInAsync("sales@onyx.test", StrongPassword);
        await LoginAsync("sales@onyx.test", "a wrong password value");

        var failures = await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>("/api/v1/admin/audit-logs?category=Security&action=LoginFailed", Token);
        var all = await owner.GetFromJsonAsync<PagedResult<AuditLogResponse>>("/api/v1/admin/audit-logs?pageSize=1000", Token);

        Assert.NotEmpty(failures!.Items);
        Assert.All(failures.Items, log => Assert.Equal("LoginFailed", log.Action));
        Assert.Equal(100, all!.PageSize);

        var everything = string.Join('\n', (await QueryAsync(db => db.AuditLogs)).Select(log => log.Changes ?? string.Empty));
        Assert.DoesNotContain(StrongPassword, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("a wrong password value", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(salesTokens.RefreshToken, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(salesTokens.AccessToken, everything, StringComparison.Ordinal);
        owner.Dispose();
    }

    private Task<Guid> PublishAsync(NotificationType type, string permission) =>
        InScopeAsync(async services =>
            (await services.GetRequiredService<INotificationService>().PublishAsync(new NotificationRequest(type, NotificationSeverity.Info, permission), Token)).Value);
}
