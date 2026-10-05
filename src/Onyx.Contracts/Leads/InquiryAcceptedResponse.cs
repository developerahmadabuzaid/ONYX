namespace Onyx.Contracts.Leads;

/// <summary>The submission was stored. The reference is shown to the visitor; it grants no access to anything.</summary>
public sealed record InquiryAcceptedResponse(string Reference);
