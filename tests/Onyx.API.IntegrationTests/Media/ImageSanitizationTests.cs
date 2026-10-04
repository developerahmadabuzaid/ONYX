using System.Buffers.Binary;
using System.Text;
using Onyx.Application.Media;
using Onyx.Infrastructure.Media;

namespace Onyx.API.IntegrationTests.Media;

/// <summary>Unit tests for the in-house image sanitizer (no database needed).</summary>
public sealed class ImageSanitizationTests
{
    private static readonly ImageInspector Inspector = new();
    private static readonly byte[] Png1x1 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private static readonly byte[] Payload = Encoding.ASCII.GetBytes("<script>alert(1)</script>");

    [Fact]
    public void Gif_keeps_frames_timing_and_looping_but_drops_comments_xmp_and_trailing_data()
    {
        var gif = Gif(frames: 2, withMetadata: true, trailing: Payload);

        var cleaned = Assert.IsType<InspectedImage>(Inspector.InspectAndClean(gif, MediaLimits.Default).Value);

        Assert.Equal("image/gif", cleaned.ContentType);
        Assert.Equal((1, 1), (cleaned.Width, cleaned.Height));
        Assert.True(Contains(cleaned.Content, "NETSCAPE2.0"));
        Assert.False(Contains(cleaned.Content, "secret comment"));
        Assert.False(Contains(cleaned.Content, "XMP DataXMP"));
        Assert.False(Contains(cleaned.Content, "<script>"));
        Assert.Equal(0x3B, cleaned.Content[^1]);
        Assert.Equal(2, cleaned.Content.Count(value => value == 0x2C));
        Assert.Equal(2, CountSequence(cleaned.Content, [0x21, 0xF9]));
    }

    [Fact]
    public void Gif_frames_are_capped_and_held_to_the_pixel_limits()
    {
        var tooMany = Inspector.InspectAndClean(Gif(frames: 3), MediaLimits.Default with { MaxFrames = 2 });
        var hugeFrame = Inspector.InspectAndClean(Gif(frames: 1, frameWidth: 9_000), MediaLimits.Default);

        Assert.Equal("Media.TooManyFrames", tooMany.Error!.Code);
        Assert.Equal("Media.TooManyFrames", hugeFrame.Error!.Code);
    }

    [Fact]
    public void Malformed_gifs_are_rejected()
    {
        var truncated = Gif(frames: 1)[..^6];
        var noImage = Gif(frames: 0);
        var unknownBlock = Gif(frames: 1);
        unknownBlock[^1] = 0x99;

        Assert.Equal("Media.Corrupt", Inspector.InspectAndClean(truncated, MediaLimits.Default).Error!.Code);
        Assert.Equal("Media.Corrupt", Inspector.InspectAndClean(noImage, MediaLimits.Default).Error!.Code);
        Assert.Equal("Media.Corrupt", Inspector.InspectAndClean(unknownBlock, MediaLimits.Default).Error!.Code);
    }

    [Fact]
    public void Jpeg_drops_exif_comments_between_scans_and_data_after_the_end_marker()
    {
        var jpeg = Jpeg(trailing: Payload);

        var cleaned = Inspector.InspectAndClean(jpeg, MediaLimits.Default).Value;

        Assert.Equal((1, 1), (cleaned.Width, cleaned.Height));
        Assert.True(Contains(cleaned.Content, "JFIF"));
        Assert.False(Contains(cleaned.Content, "Exif"));
        Assert.False(Contains(cleaned.Content, "between scans"));
        Assert.False(Contains(cleaned.Content, "<script>"));
        Assert.Equal([0xFF, 0xD9], cleaned.Content[^2..]);
        Assert.Equal(2, CountSequence(cleaned.Content, [0xFF, 0xDA]));
        Assert.True(Contains(cleaned.Content, [0x12, 0xFF, 0x00, 0x34]), "Stuffed bytes in scan data must be kept.");
    }

    [Fact]
    public void Png_keeps_only_rendering_chunks_and_nothing_after_iend()
    {
        var iend = Png1x1.Length - 12;
        var withExtras = Png1x1[..iend].Concat(Chunk("tEXt", "Author\0someone")).Concat(Chunk("prIv", "private data")).Concat(Png1x1[iend..]).Concat(Payload).ToArray();

        var cleaned = Inspector.InspectAndClean(withExtras, MediaLimits.Default).Value;

        Assert.Equal(Png1x1, cleaned.Content);
    }

    [Fact]
    public void WebP_drops_metadata_unknown_chunks_and_bytes_beyond_the_riff_size()
    {
        var image = WebPChunk("VP8L", [0x2F, 0x00, 0x00, 0x00, 0x00]);
        var exif = WebPChunk("EXIF", Encoding.ASCII.GetBytes("Exif GPS"));
        var unknown = WebPChunk("ZZZZ", Encoding.ASCII.GetBytes("hidden"));
        var body = Encoding.ASCII.GetBytes("WEBP").Concat(image).Concat(exif).Concat(unknown).ToArray();
        var riff = Encoding.ASCII.GetBytes("RIFF").Concat(BitConverter.GetBytes(body.Length)).Concat(body).Concat(Payload).ToArray();

        var cleaned = Inspector.InspectAndClean(riff, MediaLimits.Default).Value;

        Assert.False(Contains(cleaned.Content, "Exif GPS"));
        Assert.False(Contains(cleaned.Content, "hidden"));
        Assert.False(Contains(cleaned.Content, "<script>"));
        Assert.Equal(cleaned.Content.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(cleaned.Content.AsSpan(4)));
    }

    [Fact]
    public void Svg_and_disguised_files_are_rejected()
    {
        Assert.Equal("Media.UnsupportedType", Inspector.InspectAndClean(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), MediaLimits.Default).Error!.Code);
        Assert.Equal("Media.UnsupportedType", Inspector.InspectAndClean(Encoding.UTF8.GetBytes("GIF8 not really"), MediaLimits.Default).Error!.Code);
    }

    private static byte[] Gif(int frames, bool withMetadata = false, int frameWidth = 1, byte[]? trailing = null)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes("GIF89a"));
        bytes.AddRange([0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00]); // 1×1 screen, 2-colour global table.
        bytes.AddRange([0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF]);
        bytes.AddRange([0x21, 0xFF, 0x0B, .. Encoding.ASCII.GetBytes("NETSCAPE2.0"), 0x03, 0x01, 0x00, 0x00, 0x00]);
        if (withMetadata)
        {
            var comment = Encoding.ASCII.GetBytes("secret comment");
            bytes.AddRange([0x21, 0xFE, (byte)comment.Length, .. comment, 0x00]);
            bytes.AddRange([0x21, 0xFF, 0x0B, .. Encoding.ASCII.GetBytes("XMP DataXMP"), 0x04, (byte)'x', (byte)'m', (byte)'p', (byte)'!', 0x00]);
        }

        for (var frame = 0; frame < frames; frame++)
        {
            bytes.AddRange([0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00]); // Graphic control: 100 ms delay.
            bytes.AddRange([0x2C, 0x00, 0x00, 0x00, 0x00, (byte)(frameWidth & 0xFF), (byte)(frameWidth >> 8), 0x01, 0x00, 0x00]);
            bytes.AddRange([0x02, 0x02, 0x44, 0x01, 0x00]); // LZW minimum code size, one data sub-block, terminator.
        }

        bytes.Add(0x3B);
        bytes.AddRange(trailing ?? []);
        return [.. bytes];
    }

    private static byte[] Jpeg(byte[] trailing)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x10, .. Encoding.ASCII.GetBytes("JFIF"), 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);
        var exif = Encoding.ASCII.GetBytes("Exif\0\0GPS data");
        bytes.AddRange([0xFF, 0xE1, 0x00, (byte)(exif.Length + 2), .. exif]);
        bytes.AddRange([0xFF, 0xC2, 0x00, 0x11, 0x08, 0x00, 0x01, 0x00, 0x01, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]);
        byte[] scanHeader = [0xFF, 0xDA, 0x00, 0x0C, 0x03, 0x01, 0x00, 0x02, 0x11, 0x03, 0x11, 0x00, 0x3F, 0x00];
        bytes.AddRange(scanHeader);
        bytes.AddRange([0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56]);
        var comment = Encoding.ASCII.GetBytes("between scans");
        bytes.AddRange([0xFF, 0xFE, 0x00, (byte)(comment.Length + 2), .. comment]);
        bytes.AddRange(scanHeader);
        bytes.AddRange([0x78, 0x9A]);
        bytes.AddRange([0xFF, 0xD9]);
        bytes.AddRange(trailing);
        return [.. bytes];
    }

    private static byte[] Chunk(string type, string data)
    {
        var payload = Encoding.ASCII.GetBytes(data);
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        return [.. length, .. Encoding.ASCII.GetBytes(type), .. payload, 0, 0, 0, 0];
    }

    private static byte[] WebPChunk(string type, byte[] data)
    {
        var chunk = Encoding.ASCII.GetBytes(type).Concat(BitConverter.GetBytes(data.Length)).Concat(data).ToList();
        if (data.Length % 2 == 1)
        {
            chunk.Add(0);
        }

        return [.. chunk];
    }

    private static bool Contains(byte[] haystack, string needle) => Contains(haystack, Encoding.ASCII.GetBytes(needle));

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    private static int CountSequence(byte[] haystack, byte[] needle)
    {
        var count = 0;
        var span = haystack.AsSpan();
        int index;
        while ((index = span.IndexOf(needle)) >= 0)
        {
            count++;
            span = span[(index + needle.Length)..];
        }

        return count;
    }
}
