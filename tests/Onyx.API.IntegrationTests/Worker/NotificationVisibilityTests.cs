using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;

namespace Onyx.API.IntegrationTests.Worker;

public sealed class NotificationVisibilityTests(PostgresFixture fixture) : WorkerDatabaseTestBase(fixture)
{
    private static readonly NotificationViewer SalesUser =
        new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000a"), new HashSet<string> { Permissions.Leads.View });

    private static readonly NotificationViewer OperationsUser =
        new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000b"), new HashSet<string> { Permissions.SystemJobs.View });

    private static readonly NotificationViewer SecondSalesUser =
        new(Guid.Parse("0199a1b2-0000-7000-8000-00000000000c"), new HashSet<string> { Permissions.Leads.View });

    [Fact]
    public async Task Users_see_only_notifications_their_permissions_allow()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost();
        await PublishAsync(host, NotificationType.NewLead, Permissions.Leads.View);
        await PublishAsync(host, NotificationType.JobFailed, Permissions.SystemJobs.View);

        var sales = await ListAsync(host, SalesUser);
        var operations = await ListAsync(host, OperationsUser);

        Assert.Equal(nameof(NotificationType.NewLead), Assert.Single(sales.Items).Type);
        Assert.Equal(nameof(NotificationType.JobFailed), Assert.Single(operations.Items).Type);
        Assert.Equal(1, await CountUnreadAsync(host, SalesUser));
    }

    [Fact]
    public async Task Marking_a_hidden_notification_read_reveals_nothing_and_changes_nothing()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost();
        var jobFailedId = await PublishAsync(host, NotificationType.JobFailed, Permissions.SystemJobs.View);

        var result = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().MarkReadAsync(SalesUser, jobFailedId, Token));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Empty(await ReadAsync(db => db.NotificationReadStates));
    }

    [Fact]
    public async Task Read_state_is_tracked_per_user()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost();
        var leadId = await PublishAsync(host, NotificationType.NewLead, Permissions.Leads.View);

        var result = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().MarkReadAsync(SalesUser, leadId, Token));
        var again = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().MarkReadAsync(SalesUser, leadId, Token));

        Assert.True(result.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.True(Assert.Single((await ListAsync(host, SalesUser)).Items).IsRead);
        Assert.False(Assert.Single((await ListAsync(host, SecondSalesUser)).Items).IsRead);
        Assert.Equal(0, await CountUnreadAsync(host, SalesUser));
        Assert.Equal(1, await CountUnreadAsync(host, SecondSalesUser));
        Assert.Single(await ReadAsync(db => db.NotificationReadStates));
    }

    [Fact]
    public async Task Mark_all_read_only_touches_visible_notifications()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost();
        await PublishAsync(host, NotificationType.NewLead, Permissions.Leads.View);
        await PublishAsync(host, NotificationType.NewBooking, Permissions.Leads.View);
        await PublishAsync(host, NotificationType.JobFailed, Permissions.SystemJobs.View);

        var marked = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().MarkAllReadAsync(SalesUser, Token));

        Assert.Equal(2, marked);
        Assert.Equal(1, await CountUnreadAsync(host, OperationsUser));
        Assert.Empty((await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().ListAsync(SalesUser, unreadOnly: true, 1, 20, Token))).Items);
    }

    [Fact]
    public async Task Listing_is_newest_first_and_paged_with_a_capped_page_size()
    {
        Fixture.RequireDatabase();
        await using var host = CreateHost();
        for (var i = 0; i < 3; i++)
        {
            await PublishAsync(host, NotificationType.NewLead, Permissions.Leads.View);
            Time.Advance(TimeSpan.FromMinutes(1));
        }

        var page = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().ListAsync(SalesUser, unreadOnly: false, 1, 2, Token));
        var oversized = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().ListAsync(SalesUser, unreadOnly: false, 1, 10_000, Token));

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.Items[0].CreatedAtUtc > page.Items[1].CreatedAtUtc);
        Assert.Equal(NotificationService.MaxPageSize, oversized.PageSize);
    }

    private WorkerTestHost CreateHost() => WorkerTestHost.Create(ConnectionString, "worker-a", Time);

    private static async Task<Guid> PublishAsync(WorkerTestHost host, NotificationType type, string permission)
    {
        var result = await host.InScopeAsync(services =>
            services.GetRequiredService<INotificationService>().PublishAsync(new NotificationRequest(type, NotificationSeverity.Info, permission), Token));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<Onyx.Contracts.Common.PagedResult<Onyx.Contracts.Notifications.AdminNotificationResponse>> ListAsync(WorkerTestHost host, NotificationViewer viewer) =>
        host.InScopeAsync(services => services.GetRequiredService<INotificationService>().ListAsync(viewer, unreadOnly: false, 1, 20, Token));

    private static Task<int> CountUnreadAsync(WorkerTestHost host, NotificationViewer viewer) =>
        host.InScopeAsync(services => services.GetRequiredService<INotificationService>().CountUnreadAsync(viewer, Token));
}
