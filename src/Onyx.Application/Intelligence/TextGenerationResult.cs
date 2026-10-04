namespace Onyx.Application.Intelligence;

/// <summary>Untrusted generated text: sanitized/structured before use and always reviewed by an editor.</summary>
public sealed record TextGenerationResult(string Text, string ProviderKey, string? ProviderModel, IReadOnlyList<string> NeedsInformation);
