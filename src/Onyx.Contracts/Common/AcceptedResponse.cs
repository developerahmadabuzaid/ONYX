namespace Onyx.Contracts.Common;

/// <summary>Returned when work was queued (e.g. an email) rather than completed synchronously.</summary>
public sealed record AcceptedResponse(Guid Id);
