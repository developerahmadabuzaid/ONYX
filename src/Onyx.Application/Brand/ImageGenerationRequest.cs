namespace Onyx.Application.Brand;

/// <summary>Input for a future image provider. <see cref="Branding"/> comes only from <see cref="BrandedImagePolicy"/>.</summary>
public sealed record ImageGenerationRequest(ImagePurpose Purpose, string LanguageCode, string Prompt, BrandingInstructions? Branding);
