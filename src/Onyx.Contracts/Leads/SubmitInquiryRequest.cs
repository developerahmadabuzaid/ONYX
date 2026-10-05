namespace Onyx.Contracts.Leads;

/// <summary>A public form submission. <see cref="Type"/> is one of <see cref="InquiryTypes.PublicForms"/>.</summary>
public sealed record SubmitInquiryRequest(string Type, InquiryFormDto Form);
