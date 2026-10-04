using System.Buffers.Binary;
using System.Text;
using Onyx.Application.Media;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Media;

/// <summary>
/// In-house image identification (no imaging package): the type comes from the file signature, never the name or the
/// client's claim. JPEG, PNG and WebP have embedded metadata removed (EXIF, XMP, IPTC, comments, text chunks — these can
/// carry GPS location and device details). SVG and every other format are rejected.
/// Re-encoding and responsive variants need an approved imaging library and are deferred.
/// </summary>
internal sealed class ImageInspector : IImageInspector
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly HashSet<string> PngDroppedChunks = new(StringComparer.Ordinal) { "tEXt", "zTXt", "iTXt", "eXIf", "tIME" };

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
                _ when content.Length >= 10 && (Ascii(content, 0, 6) is "GIF87a" or "GIF89a") =>
                    new InspectedImage(content, "image/gif", "gif", BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6)), BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(8))),
                _ => null,
            };

            if (parsed is null)
            {
                return Error.Validation("Media.UnsupportedType", "Only JPEG, PNG, WebP and GIF images are accepted (SVG and other formats are not).");
            }

            if (parsed.Width <= 0 || parsed.Height <= 0 || parsed.Width > limits.MaxWidth || parsed.Height > limits.MaxHeight
                || (long)parsed.Width * parsed.Height > limits.MaxPixels)
            {
                return Error.Validation("Media.Dimensions", $"Images may be at most {limits.MaxWidth}×{limits.MaxHeight} pixels.");
            }

            return parsed;
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException or InvalidDataException)
        {
            return Error.Validation("Media.Corrupt", "The image file is damaged or not what it claims to be.");
        }
    }

    private static InspectedImage? Png(byte[] content)
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

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset + 8));
                height = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset + 12));
            }

            if (!PngDroppedChunks.Contains(type))
            {
                output.Write(content, offset, 12 + length);
            }

            sawEnd = type == "IEND";
            offset += 12 + length;
        }

        return width is null || !sawEnd ? throw new InvalidDataException("Incomplete PNG.") : new InspectedImage(output.ToArray(), "image/png", "png", width.Value, height!.Value);
    }

    private static InspectedImage? Jpeg(byte[] content)
    {
        using var output = new MemoryStream(content.Length);
        output.Write(content, 0, 2);
        int? width = null;
        int? height = null;
        var offset = 2;

        while (offset + 4 <= content.Length)
        {
            if (content[offset] != 0xFF)
            {
                throw new InvalidDataException("Invalid JPEG marker.");
            }

            var marker = content[offset + 1];
            if (marker == 0xFF)
            {
                offset++;
                continue;
            }

            if (marker == 0xDA)
            {
                // Start of scan: the rest is entropy-coded image data (and the end marker), copied as is.
                output.Write(content, offset, content.Length - offset);
                break;
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

            // Drop APP1 (EXIF/XMP), APP13 (IPTC/Photoshop) and comments; keep JFIF, ICC colour profiles and Adobe markers.
            var drop = marker is 0xE1 or 0xED or 0xFE;
            if (!drop)
            {
                output.Write(content, offset, 2 + length);
            }

            offset += 2 + length;
        }

        return width is null ? throw new InvalidDataException("No JPEG frame header.") : new InspectedImage(output.ToArray(), "image/jpeg", "jpg", width.Value, height!.Value);
    }

    private static bool IsWebP(byte[] content) =>
        content.Length >= 20 && Ascii(content, 0, 4) == "RIFF" && Ascii(content, 8, 4) == "WEBP";

    private static InspectedImage? WebP(byte[] content)
    {
        var chunks = new List<(string Type, int Offset, int Length)>();
        int? width = null;
        int? height = null;
        var offset = 12;

        while (offset + 8 <= content.Length)
        {
            var type = Ascii(content, offset, 4);
            var length = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset + 4));
            if (length < 0 || offset + 8L + length > content.Length)
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
        foreach (var (type, chunkOffset, length) in chunks.Where(chunk => chunk.Type is not ("EXIF" or "XMP ")))
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

    private static string Ascii(byte[] content, int offset, int length) => Encoding.ASCII.GetString(content, offset, length);
}
