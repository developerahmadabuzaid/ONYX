using Onyx.Domain.Notifications;

namespace Onyx.Domain.Tests.Notifications;

public sealed class AdminNotificationTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_keeps_type_permission_and_parameters()
    {
        var notification = AdminNotification.Create(
            UtcNow, NotificationType.JobFailed, NotificationSeverity.Warning, "System.Jobs.View",
            new Dictionary<string, string> { ["job"] = "operational-cleanup" }, "Job", "operational-cleanup");

        Assert.Equal(NotificationType.JobFailed, notification.Type);
        Assert.Equal("System.Jobs.View", notification.RequiredPermission);
        Assert.Equal("operational-cleanup", notification.Parameters["job"]);
        Assert.Equal("Job", notification.ResourceType);
    }

    [Fact]
    public void Required_permission_is_mandatory()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            AdminNotification.Create(UtcNow, NotificationType.NewLead, NotificationSeverity.Info, " "));
    }

    [Fact]
    public void Non_utc_timestamps_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdminNotification.Create(UtcNow.ToOffset(TimeSpan.FromHours(3)), NotificationType.NewLead, NotificationSeverity.Info, "Leads.View"));
    }

    [Fact]
    public void Too_many_parameters_are_rejected_and_long_values_truncated()
    {
        var tooMany = Enumerable.Range(0, AdminNotification.MaxParameters + 1).ToDictionary(i => $"k{i}", i => "v");
        Assert.Throws<ArgumentException>(() =>
            AdminNotification.Create(UtcNow, NotificationType.NewLead, NotificationSeverity.Info, "Leads.View", tooMany));

        var notification = AdminNotification.Create(
            UtcNow, NotificationType.NewLead, NotificationSeverity.Info, "Leads.View",
            new Dictionary<string, string> { ["source"] = new string('x', 500) });
        Assert.Equal(AdminNotification.ParameterValueMaxLength, notification.Parameters["source"].Length);
    }
}
