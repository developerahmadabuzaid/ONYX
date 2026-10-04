namespace Onyx.Application.Media;

public sealed record InspectedImage(byte[] Content, string ContentType, string Extension, int Width, int Height);
