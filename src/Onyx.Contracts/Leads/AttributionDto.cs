namespace Onyx.Contracts.Leads;

/// <summary>
/// Origin context collected by Onyx.Web. <see cref="FirstTouch"/> and <see cref="AnalyticsClientId"/> are honoured only when
/// <see cref="AnalyticsConsent"/> is true; without consent the API keeps only the context of the submitting session.
/// </summary>
public sealed record AttributionDto(
    TouchPointDto? FirstTouch,
    TouchPointDto? LastTouch,
    string? SubmissionPath,
    string? Market,
    string? DeviceClass,
    string? AnalyticsClientId,
    bool AnalyticsConsent);
