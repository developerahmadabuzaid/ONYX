using System.Buffers.Binary;
using System.Text;
using Onyx.Application.Media;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Media;

/// <summary>
/// In-house image identification and metadata removal (no imaging package): the type comes from the file signature, never the
/// name or the client's claim. Every format is parsed structurally and rewritten with an allow-list of parts, so metadata
/// (EXIF, XMP, IPTC, comments, text, private chunks — these can carry GPS location and device details) and any bytes after the
/// end of the image (polyglot payloads) are dropped. Colour profiles and animation data are kept. SVG and every other format
/// are rejected. Re-encoding and responsive variants need an approved imaging library and are deferred.
/// </summary>
internal sealed class ImageInspector : IImageInspector
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Critical chunks plus ancillary chunks that affect rendering (transparency, colour, animation). Everything else is dropped.</summary>
    private static readonly HashSet<string> PngKeptChunks = new(StringComparer.Ordinal)
    {
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "sBIT", "bKGD", "pHYs", "cICP", "mDCV", "cLLI", "acTL", "fcTL", "fdAT",
    };

    private static readonly HashSet<string> WebPKeptChunks = new(StringComparer.Ordinal) { "VP8 ", "VP8L", "VP8X", "ALPH", "ANIM", "ANMF", "ICCP" };

    /// <summary>GIF application extensions that carry rendering data: animation looping and ICC colour profiles.</summary>
    private static readonly HashSet<string> GifKeptApplications = new(StringComparer.Ordinal) { "NETSCAPE2.0", "ANIMEXTS1.0", "ICCRGBG1012" };

    public Result<InspectedImage> InspectAndClean(byte[] content, MediaLimits limits)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        try
        {
            var parsed = content switch
            {
                _ when content.AsSpan().StartsWith(PngSignature) => Png(content),
                [0xFF, 0xD8, 0xFF, ..] => Jpeg(content),
                _ when IsWebP(content) => WebP(content),
                _ when content.Length >= 13 && (Ascii(content, 0, 6) is "GIF87a" or "GIF89a") => Gif(content, limits),
                _ => null,
            };

            if (parsed is null)
            {
                return Error.Validation("Media.UnsupportedType", "Only JPEG, PNG, WebP and GIF images are accepted (SVG and other formats are not).");
            }

            if (!WithinLimits(parsed.Width, parsed.Height, limits))
            {
                return Error.Validation("Media.Dimensions", $"Images may be at most {limits.MaxWidth}×{limits.MaxHeight} pixels.");
            }

            return parsed;
        }
        catch (FrameLimitException)
        {
            return Error.Validation("Media.TooManyFrames", $"Animated images may have at most {limits.MaxFrames} frames, each within {limits.MaxWidth}×{limits.MaxHeight} pixels.");
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException or InvalidDataException)
        {
            return Error.Validation("Media.Corrupt", "The image file is damaged or not what it claims to be.");
        }
    }

    private static bool WithinLimits(int width, int height, MediaLimits limits) =>
        width > 0 && height > 0 && width <= limits.MaxWidth && height <= limits.MaxHeight && (long)width * height <= limits.MaxPixels;

    private static InspectedImage Png(byte[] content)
    {
        using var output = new MemoryStream(content.Length);
        output.Write(PngSignature);
        int? width = null;
        int? height = null;
        var offset = PngSignature.Length;
        var sawEnd = false;

        while (offset + 12 <= content.Length && !sawEnd)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset));
            var type = Ascii(content, offset + 4, 4);
            if (length < 0 || offset + 12L + length > content.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }

            if (width is null && type != "IHDR")
            {
                throw new InvalidDataException("PNG must start with IHDR.");
            }

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset + 8));
                height = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset + 12));
            }

            if (PngKeptChunks.Contains(type))
            {
                output.Write(content, offset, 12 + length);
            }

            sawEnd = type == "IEND";
            offset += 12 + length;
        }

        // Anything after IEND is not part of the image and is dropped.
        return width is null || !sawEnd ? throw new InvalidDataException("Incomplete PNG.") : new InspectedImage(output.ToArray(), "image/png", "png", width.Value, height!.Value);
    }

    private static InspectedImage Jpeg(byte[] content)
    {
        using var output = new MemoryStream(content.Length);
        output.Write(content, 0, 2);
        int? width = null;
        int? height = null;
        var offset = 2;
        var inScan = false;

        while (offset < content.Length)
        {
            if (inScan)
            {
                // Entropy-coded data: copy up to the next real marker. FF00 is a stuffed byte, FFD0–FFD7 are restart markers,
                // FFFF is fill; any other FFxx ends the scan.
                var start = offset;
                while (offset + 1 < content.Length && !(content[offset] == 0xFF && content[offset + 1] is not 0x00 and not (>= 0xD0 and <= 0xD7) and not 0xFF))
                {
                    offset++;
                }

                if (offset + 1 >= content.Length)
                {
                    // No end marker: keep the remaining scan data as it is (some encoders omit EOI).
                    output.Write(content, start, content.Length - start);
                    break;
                }

                output.Write(content, start, offset - start);
                inScan = false;
            }

            if (offset + 2 > content.Length || content[offset] != 0xFF)
            {
                throw new InvalidDataException("Invalid JPEG marker.");
            }

            var marker = content[offset + 1];
            if (marker == 0xFF)
            {
                offset++;
                continue;
            }

            if (marker == 0xD9)
            {
                // End of image: anything after it is not part of the image and is dropped.
                output.Write(content, offset, 2);
                break;
            }

            if (offset + 4 > content.Length)
            {
                throw new InvalidDataException("Truncated JPEG segment.");
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 2));
            if (length < 2 || offset + 2 + length > content.Length)
            {
                throw new InvalidDataException("Truncated JPEG segment.");
            }

            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 5));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 7));
            }

            // Drop APP1 (EXIF/XMP), APP13 (IPTC/Photoshop), other vendor APPn blocks and comments, wherever they appear (also
            // between progressive scans). Keep JFIF (APP0), ICC colour profiles (APP2) and Adobe colour transform (APP14).
            var drop = marker is (>= 0xE1 and <= 0xEF and not 0xE2 and not 0xEE) or 0xFE;
            if (!drop)
            {
                output.Write(content, offset, 2 + length);
            }

            offset += 2 + length;
            inScan = marker == 0xDA;
        }

        return width is null ? throw new InvalidDataException("No JPEG frame header.") : new InspectedImage(output.ToArray(), "image/jpeg", "jpg", width.Value, height!.Value);
    }

    private static bool IsWebP(byte[] content) =>
        content.Length >= 20 && Ascii(content, 0, 4) == "RIFF" && Ascii(content, 8, 4) == "WEBP";

    private static InspectedImage WebP(byte[] content)
    {
        // Only the declared RIFF payload is the image; bytes beyond it are dropped.
        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(4));
        var end = (int)Math.Min(content.Length, 8L + riffSize);
        var chunks = new List<(string Type, int Offset, int Length)>();
        int? width = null;
        int? height = null;
        var offset = 12;

        while (offset + 8 <= end)
        {
            var type = Ascii(content, offset, 4);
            var length = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset + 4));
            if (length < 0 || offset + 8L + length > end)
            {
                throw new InvalidDataException("Truncated WebP chunk.");
            }

            var data = content.AsSpan(offset + 8, length);
            switch (type)
            {
                case "VP8X" when length >= 10:
                    width = 1 + (data[4] | (data[5] << 8) | (data[6] << 16));
                    height = 1 + (data[7] | (data[8] << 8) | (data[9] << 16));
                    break;
                case "VP8 " when length >= 10 && width is null:
                    width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) & 0x3FFF;
                    height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]) & 0x3FFF;
                    break;
                case "VP8L" when length >= 5 && width is null && data[0] == 0x2F:
                    var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
                    width = 1 + (int)(bits & 0x3FFF);
                    height = 1 + (int)((bits >> 14) & 0x3FFF);
                    break;
            }

            chunks.Add((type, offset, length));
            offset += 8 + length + (length & 1);
        }

        if (width is null)
        {
            throw new InvalidDataException("No WebP image header.");
        }

        using var output = new MemoryStream(content.Length);
        output.Write(Encoding.ASCII.GetBytes("RIFF"));
        output.Write(new byte[4]);
        output.Write(Encoding.ASCII.GetBytes("WEBP"));
        foreach (var (type, chunkOffset, length) in chunks.Where(chunk => WebPKeptChunks.Contains(chunk.Type)))
        {
            var chunk = content.AsSpan(chunkOffset, 8 + length).ToArray();
            if (type == "VP8X")
            {
                chunk[8] &= 0xF3; // Clear the EXIF (0x08) and XMP (0x04) presence flags.
            }

            output.Write(chunk);
            if ((length & 1) == 1)
            {
                output.WriteByte(0);
            }
        }

        var cleaned = output.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(cleaned.AsSpan(4), cleaned.Length - 8);
        return new InspectedImage(cleaned, "image/webp", "webp", width.Value, height!.Value);
    }

    /// <summary>
    /// GIF: keeps the header, screen descriptor, colour tables, image frames, graphic-control (timing/transparency) and the
    /// looping / colour-profile application extensions. Drops comments, plain-text and other application extensions (e.g. XMP)
    /// and everything after the trailer. Every frame must fit the pixel limits, and the frame count is capped.
    /// </summary>
    private static InspectedImage Gif(byte[] content, MediaLimits limits)
    {
        using var output = new MemoryStream(content.Length);
        var width = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(8));
        var screenFlags = content[10];
        var offset = 13 + ColorTableSize(screenFlags);
        if (offset > content.Length)
        {
            throw new InvalidDataException("Truncated GIF colour table.");
        }

        output.Write(content, 0, offset);
        var frames = 0;
        var sawTrailer = false;

        while (offset < content.Length && !sawTrailer)
        {
            switch (content[offset])
            {
                case 0x2C:
                    if (offset + 10 > content.Length)
                    {
                        throw new InvalidDataException("Truncated GIF image descriptor.");
                    }

                    var frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset + 5));
                    var frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offset + 7));
                    if (++frames > limits.MaxFrames || !WithinLimits(frameWidth, frameHeight, limits))
                    {
                        throw new FrameLimitException();
                    }

                    var dataStart = offset + 10 + ColorTableSize(content[offset + 9]);
                    if (dataStart >= content.Length || content[dataStart] is < 1 or > 11)
                    {
                        throw new InvalidDataException("Invalid GIF image data.");
                    }

                    var frameEnd = SkipSubBlocks(content, dataStart + 1);
                    output.Write(content, offset, frameEnd - offset);
                    offset = frameEnd;
                    break;

                case 0x21:
                    if (offset + 2 > content.Length)
                    {
                        throw new InvalidDataException("Truncated GIF extension.");
                    }

                    var label = content[offset + 1];
                    var extensionEnd = SkipSubBlocks(content, offset + 2);
                    var keep = label switch
                    {
                        0xF9 => true,
                        0xFF => offset + 14 <= content.Length && content[offset + 2] == 11 && GifKeptApplications.Contains(Ascii(content, offset + 3, 11)),
                        _ => false, // 0xFE comment, 0x01 plain text and unknown extensions.
                    };
                    if (keep)
                    {
                        output.Write(content, offset, extensionEnd - offset);
                    }

                    offset = extensionEnd;
                    break;

                case 0x3B:
                    output.WriteByte(0x3B);
                    sawTrailer = true; // Anything after the trailer is dropped.
                    break;

                default:
                    throw new InvalidDataException("Unknown GIF block.");
            }
        }

        if (frames == 0)
        {
            throw new InvalidDataException("GIF without an image.");
        }

        if (!sawTrailer)
        {
            output.WriteByte(0x3B);
        }

        return new InspectedImage(output.ToArray(), "image/gif", "gif", width, height);
    }

    private static int ColorTableSize(byte flags) => (flags & 0x80) == 0 ? 0 : 3 * (1 << ((flags & 0x07) + 1));

    /// <summary>Returns the offset just after a run of GIF data sub-blocks (each length-prefixed, ended by a zero-length block).</summary>
    private static int SkipSubBlocks(byte[] content, int offset)
    {
        while (true)
        {
            if (offset >= content.Length)
            {
                throw new InvalidDataException("Truncated GIF data.");
            }

            var size = content[offset];
            offset += 1 + size;
            if (size == 0)
            {
                return offset;
            }
        }
    }

    private static string Ascii(byte[] content, int offset, int length) => Encoding.ASCII.GetString(content, offset, length);

    private sealed class FrameLimitException : Exception
    {
    }
}
