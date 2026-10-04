namespace Onyx.Application.Media;

/// <summary>An opened media file for streaming. The caller disposes <see cref="Content"/>.</summary>
public sealed record MediaFile(Stream Content, string ContentType, string FileName, long Length, string Sha256);
