namespace Onyx.Contracts.Leads;

/// <summary>The attribution trail of one inquiry, as stored (consent-aware).</summary>
public sealed record OriginResponse(
    TouchPointDto? FirstTouch,
    TouchPointDto? LastTouch,
    string? SubmissionPath,
    string LanguageCode,
    string? Market,
    string? DeviceClass,
    string? ToolSource,
    Guid? ToolRunId,
    string? AnalyticsClientId,
    bool AnalyticsConsent);
