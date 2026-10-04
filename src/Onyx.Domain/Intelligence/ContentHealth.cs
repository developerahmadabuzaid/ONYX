namespace Onyx.Domain.Intelligence;

/// <summary>Content health as assessed from real data. <see cref="InsufficientData"/> whenever the data does not exist.</summary>
public enum ContentHealth
{
    InsufficientData = 0,
    Healthy = 1,
    Opportunity = 2,
    NeedsUpdate = 3,
    Declining = 4,
    ReviewRequired = 5,
}
