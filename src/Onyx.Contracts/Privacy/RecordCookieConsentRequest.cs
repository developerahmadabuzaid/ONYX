namespace Onyx.Contracts.Privacy;

/// <summary>Sent by Onyx.Web when a visitor saves cookie choices. Necessary cookies are always on and not part of the choice.</summary>
public sealed record RecordCookieConsentRequest(Guid ConsentId, string PolicyVersion, bool Analytics, bool Marketing, string LanguageCode);
