namespace Onyx.Infrastructure.Email.Templates;

/// <summary>A single call-to-action link. Only absolute https URLs are rendered (http is allowed for localhost during development).</summary>
public sealed record EmailAction(string Label, Uri Url);
