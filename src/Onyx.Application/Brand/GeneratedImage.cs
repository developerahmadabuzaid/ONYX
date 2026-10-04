namespace Onyx.Application.Brand;

/// <summary>A generated image awaiting editor review; it enters the media library only through the normal upload checks.</summary>
public sealed record GeneratedImage(byte[] Content, string ProviderKey, string? ProviderModel);
