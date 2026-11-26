namespace Onyx.Contracts.Customers;

public sealed record StartAiDemoRequest(string ProductSlug, string Language, string Prompt, string? CaptchaToken);
