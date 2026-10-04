using System.Buffers.Binary;
using Infrastructure.Files;
using Shouldly;

namespace Cms.Tests.Storage;

/// <summary>
/// Covers the header parsing that gives every uploaded image its pixel size.
/// <para>
/// Those numbers are not cosmetic: a responsive <c>srcset</c> is a list of widths,
/// so an image whose size is read wrongly (or not at all) either drops out of the
/// set or tells the browser to pick the wrong file. The headers are built here
/// byte by byte rather than checked in as binary fixtures, so what each test
/// asserts about the format is readable in the test itself.
/// </para>
/// </summary>
public sealed class ImageDimensionReaderTests
{
    private static (int Width, int Height)? Read(byte[] bytes) =>
        ImageDimensionReader.TryRead(new MemoryStream(bytes));

    // ── PNG ──────────────────────────────────────────────────────────────────

    private static byte[] BuildPng(int width, int height)
    {
        byte[] bytes = new byte[24];
        // Signature, then a 4-byte chunk length and the IHDR type.
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    [Fact]
    public void Reads_png_dimensions()
        => Read(BuildPng(1920, 1080)).ShouldBe((1920, 1080));

    // ── GIF ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Reads_gif_dimensions()
    {
        byte[] bytes = new byte[10];
        "GIF89a"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 640);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 480);

        Read(bytes).ShouldBe((640, 480));
    }

    // ── BMP ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A negative height is legal and means the rows are stored top-down — the
    /// picture is still that many pixels tall.
    /// </summary>
    [Fact]
    public void Reads_bmp_dimensions_including_top_down_rows()
    {
        byte[] bytes = new byte[26];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 300);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), -200);

        Read(bytes).ShouldBe((300, 200));
    }

    // ── WebP ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reads_lossy_webp_dimensions()
    {
        byte[] bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8 "u8.CopyTo(bytes.AsSpan(12));
        // 14-bit width and height; the top two bits are scaling flags and ignored.
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 800);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), 600);

        Read(bytes).ShouldBe((800, 600));
    }

    [Fact]
    public void Reads_extended_webp_dimensions()
    {
        byte[] bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8X"u8.CopyTo(bytes.AsSpan(12));
        // 24-bit width-1 and height-1.
        WriteUInt24(bytes.AsSpan(24), 1024 - 1);
        WriteUInt24(bytes.AsSpan(27), 768 - 1);

        Read(bytes).ShouldBe((1024, 768));
    }

    private static void WriteUInt24(Span<byte> target, int value)
    {
        target[0] = (byte)(value & 0xFF);
        target[1] = (byte)((value >> 8) & 0xFF);
        target[2] = (byte)((value >> 16) & 0xFF);
    }

    // ── JPEG ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The size lives in a Start-Of-Frame segment that can sit behind any amount of
    /// metadata, which is why the parser walks the segment chain instead of reading
    /// a fixed offset. This builds a file with an EXIF-sized APP1 segment in front
    /// of the frame header to prove the walk works.
    /// </summary>
    [Fact]
    public void Reads_jpeg_dimensions_behind_a_metadata_segment()
    {
        const int metadataLength = 120;
        List<byte> bytes = [0xFF, 0xD8];

        // APP1 segment: marker, 2-byte length (including the length bytes), payload.
        bytes.AddRange([0xFF, 0xE1]);
        bytes.AddRange([(byte)((metadataLength >> 8) & 0xFF), (byte)(metadataLength & 0xFF)]);
        bytes.AddRange(new byte[metadataLength - 2]);

        // SOF0: marker, length, precision, height, width.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([0x02, 0x1C]); // height 540
        bytes.AddRange([0x03, 0xC0]); // width 960

        Read([.. bytes]).ShouldBe((960, 540));
    }

    /// <summary>
    /// 0xC4 sits inside the SOF marker range but is a Huffman table, not a frame
    /// header. Reading it as one would produce dimensions out of table data.
    /// </summary>
    [Fact]
    public void Does_not_mistake_a_huffman_table_for_a_frame_header()
    {
        List<byte> bytes = [0xFF, 0xD8];

        // DHT (0xC4) with a payload that would look like a plausible size. The
        // length field counts itself plus the payload — 2 + 5 here — so that the
        // parser skips exactly this segment and lands on the frame header.
        bytes.AddRange([0xFF, 0xC4, 0x00, 0x07, 0x00, 0x11, 0x11, 0x22, 0x22]);

        // The real frame header, with the size that must win.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange([0x00, 0x64]); // height 100
        bytes.AddRange([0x00, 0xC8]); // width 200

        Read([.. bytes]).ShouldBe((200, 100));
    }

    // ── Formats and inputs with no answer ────────────────────────────────────

    /// <summary>
    /// An unreadable header must never fail an upload — the file is stored, it
    /// simply gets no dimensions and so takes no part in a srcset.
    /// </summary>
    [Fact]
    public void Returns_null_for_content_that_is_not_a_known_image()
        => Read("this is not an image"u8.ToArray()).ShouldBeNull();

    [Fact]
    public void Returns_null_for_an_empty_stream()
        => Read([]).ShouldBeNull();

    [Fact]
    public void Returns_null_for_a_truncated_header()
        => Read(BuildPng(100, 100)[..12]).ShouldBeNull();

    /// <summary>
    /// A zero dimension is not a usable width for a srcset entry, so it is treated
    /// as "unknown" rather than passed through.
    /// </summary>
    [Fact]
    public void Returns_null_when_a_dimension_is_zero()
        => Read(BuildPng(0, 500)).ShouldBeNull();

    /// <summary>
    /// The caller hands the same stream straight to storage afterwards, so reading
    /// the header must not consume it.
    /// </summary>
    [Fact]
    public void Rewinds_the_stream_so_the_caller_can_still_store_it()
    {
        using var stream = new MemoryStream(BuildPng(320, 240));

        ImageDimensionReader.TryRead(stream).ShouldBe((320, 240));

        stream.Position.ShouldBe(0);
    }
}
