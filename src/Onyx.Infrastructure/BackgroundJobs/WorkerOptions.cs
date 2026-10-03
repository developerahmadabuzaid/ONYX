using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Bound from the <c>Worker</c> section; validated at Worker startup.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>How long a claimed message or job stays locked; also the job execution timeout. Expired leases are reclaimable.</summary>
    [Range(10, 3_600)]
    public int LeaseSeconds { get; set; } = 120;

    [Range(1, 500)]
    public int OutboxBatchSize { get; set; } = 50;

    [Range(1, 200)]
    public int EmailBatchSize { get; set; } = 20;

    [Range(1, 50)]
    public int MaxOutboxAttempts { get; set; } = 10;

    [Range(1, 50)]
    public int MaxEmailAttempts { get; set; } = 8;

    [Range(1, 3_600)]
    public int RetryBaseDelaySeconds { get; set; } = 30;

    [Range(1, 1_440)]
    public int RetryMaxDelayMinutes { get; set; } = 60;

    /// <summary>Operational housekeeping only (no personal data); personal-data retention is handled by retention policies.</summary>
    [Range(1, 365)]
    public int ProcessedOutboxRetentionDays { get; set; } = 14;

    [Range(1, 365)]
    public int JobRunRetentionDays { get; set; } = 30;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan Lease => TimeSpan.FromSeconds(LeaseSeconds);

    public TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(RetryBaseDelaySeconds);

    public TimeSpan RetryMaxDelay => TimeSpan.FromMinutes(RetryMaxDelayMinutes);
}
