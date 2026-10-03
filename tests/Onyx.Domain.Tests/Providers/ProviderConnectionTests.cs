using Onyx.Domain.Providers;

namespace Onyx.Domain.Tests.Providers;

public sealed class ProviderConnectionTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_connections_start_not_configured()
    {
        var connection = ProviderConnection.Register("email.smtp", ProviderCategory.Email);

        Assert.Equal(ProviderConnectionStatus.NotConfigured, connection.Status);
        Assert.False(connection.IsUsable);
    }

    [Fact]
    public void Success_activates_and_clears_the_last_error()
    {
        var connection = ProviderConnection.Register("email.smtp", ProviderCategory.Email);
        connection.RecordFailure(UtcNow, "SMTP server unreachable.");

        connection.RecordSuccess(UtcNow.AddMinutes(1));

        Assert.Equal(ProviderConnectionStatus.Active, connection.Status);
        Assert.Null(connection.LastError);
        Assert.Equal(UtcNow.AddMinutes(1), connection.LastSucceededAtUtc);
    }

    [Fact]
    public void Failure_event_is_raised_only_on_transition_into_error()
    {
        var connection = ProviderConnection.Register("email.smtp", ProviderCategory.Email);

        connection.RecordFailure(UtcNow, "down");
        connection.RecordFailure(UtcNow.AddMinutes(1), "still down");

        var raised = Assert.Single(connection.DomainEvents);
        Assert.Equal("email.smtp", Assert.IsType<ProviderConnectionFailed>(raised).ProviderKey);

        connection.ClearDomainEvents();
        connection.RecordSuccess(UtcNow.AddMinutes(2));
        connection.RecordFailure(UtcNow.AddMinutes(3), "down again");
        Assert.Single(connection.DomainEvents);
    }

    [Fact]
    public void Disabled_connections_ignore_reports_until_enabled()
    {
        var connection = ProviderConnection.Register("analytics.ga4", ProviderCategory.Analytics);
        connection.Disable();

        connection.RecordFailure(UtcNow, "down");
        connection.RecordSuccess(UtcNow);
        connection.MarkNotConfigured();

        Assert.Equal(ProviderConnectionStatus.Disabled, connection.Status);
        Assert.Empty(connection.DomainEvents);

        connection.Enable();
        Assert.Equal(ProviderConnectionStatus.NotConfigured, connection.Status);
    }

    [Fact]
    public void Long_errors_are_truncated()
    {
        var connection = ProviderConnection.Register("email.smtp", ProviderCategory.Email);

        connection.RecordFailure(UtcNow, new string('e', 2_000));

        Assert.Equal(ProviderConnection.LastErrorMaxLength, connection.LastError!.Length);
    }
}
