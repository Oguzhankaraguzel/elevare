using System.Buffers.Binary;

namespace Infrastructure.Files;

/// <summary>
/// Reads pixel dimensions straight out of an image file's header, without decoding
/// the image or taking an imaging dependency.
/// <para>
/// Dimensions are what make responsive images possible at all: a <c>srcset</c> is a
/// list of "this file is N pixels wide", and the browser cannot pick between
/// variants without those numbers. They also let the public site emit
/// <c>width</c>/<c>height</c> attributes, which is what stops an image from shifting
/// the page as it loads (Cumulative Layout Shift).
/// </para>
/// <para>
/// Only the header is read — a few dozen bytes — so this costs nothing next to the
/// upload itself. Formats whose dimensions are meaningless or absent (SVG scales to
/// any size; ICO holds several sizes at once) return <c>null</c>, and the caller
/// simply stores no dimensions for them.
/// </para>
/// </summary>
internal static class ImageDimensionReader
{
    /// <summary>
    /// Enough for every header this parser understands. JPEG is the outlier: its
    /// size lives in a segment that can sit behind an arbitrary amount of EXIF/ICC
    /// metadata, so the scan below walks segment by segment and gives up once it
    /// passes this budget rather than reading a whole multi-megabyte file.
    /// </summary>
    private const int MaxHeaderBytes = 64 * 1024;

    /// <summary>
    /// Returns the image's pixel size, or <c>null</c> when the stream is not one of
    /// the understood raster formats (or is truncated/corrupt). Never throws: an
    /// unreadable header is a "no dimensions" answer, not a failed upload.
    /// </summary>
    /// <remarks>
    /// The stream is rewound to position 0 before and after reading, so the caller
    /// can hand the same stream straight to storage afterwards.
    /// </remarks>
    public static (int Width, int Height)? TryRead(Stream stream)
    {
        if (stream is null || !stream.CanRead || !stream.CanSeek)
            return null;

        try
        {
            stream.Position = 0;

            byte[] buffer = new byte[Math.Min(MaxHeaderBytes, stream.Length <= 0 ? MaxHeaderBytes : stream.Length)];
            int read = ReadAtLeast(stream, buffer);
            ReadOnlySpan<byte> head = buffer.AsSpan(0, read);

            return ReadPng(head)
                ?? ReadGif(head)
                ?? ReadBmp(head)
                ?? ReadWebp(head)
                ?? ReadJpeg(head);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException)
        {
            return null;
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = 0;
        }
    }

    private static int ReadAtLeast(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    // ── PNG ──────────────────────────────────────────────────────────────────
    // 8-byte signature, then the IHDR chunk: 4-byte length, "IHDR", then width and
    // height as big-endian 32-bit integers.
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static (int, int)? ReadPng(ReadOnlySpan<byte> b)
    {
        if (b.Length < 24) return null;
        if (!b[..8].SequenceEqual(PngSignature)) return null;
        if (!b.Slice(12, 4).SequenceEqual("IHDR"u8)) return null;

        int width = BinaryPrimitives.ReadInt32BigEndian(b.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(b.Slice(20, 4));
        return Valid(width, height);
    }

    // ── GIF ──────────────────────────────────────────────────────────────────
    // "GIF87a"/"GIF89a", then the logical screen width and height as little-endian
    // 16-bit integers.
    private static (int, int)? ReadGif(ReadOnlySpan<byte> b)
    {
        if (b.Length < 10) return null;
        if (!b[..3].SequenceEqual("GIF"u8)) return null;

        int width = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(6, 2));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(8, 2));
        return Valid(width, height);
    }

    // ── BMP ──────────────────────────────────────────────────────────────────
    // "BM", then a 14-byte file header followed by the DIB header, whose width and
    // height are signed little-endian 32-bit integers. A negative height means the
    // rows are stored top-down — the size is still |height|.
    private static (int, int)? ReadBmp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 26) return null;
        if (b[0] != (byte)'B' || b[1] != (byte)'M') return null;

        int width = BinaryPrimitives.ReadInt32LittleEndian(b.Slice(18, 4));
        int height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(b.Slice(22, 4)));
        return Valid(width, height);
    }

    // ── WebP ─────────────────────────────────────────────────────────────────
    // "RIFF" + size + "WEBP", then one of three chunk layouts, each storing the
    // size differently. All three are in use in the wild, so all three are read.
    private static (int, int)? ReadWebp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30) return null;
        if (!b[..4].SequenceEqual("RIFF"u8) || !b.Slice(8, 4).SequenceEqual("WEBP"u8)) return null;

        ReadOnlySpan<byte> chunk = b.Slice(12, 4);

        // Lossy: 14-bit width and height, after a 3-byte start code and "9d 01 2a".
        if (chunk.SequenceEqual("VP8 "u8) && b.Length >= 30)
        {
            int width = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(26, 2)) & 0x3FFF;
            int height = BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(28, 2)) & 0x3FFF;
            return Valid(width, height);
        }

        // Lossless: 14-bit width-1 and height-1 packed across bytes 21-24.
        if (chunk.SequenceEqual("VP8L"u8) && b.Length >= 25)
        {
            uint bits = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(21, 4));
            int width = (int)(bits & 0x3FFF) + 1;
            int height = (int)((bits >> 14) & 0x3FFF) + 1;
            return Valid(width, height);
        }

        // Extended (animation/alpha): 24-bit width-1 and height-1 at bytes 24 and 27.
        if (chunk.SequenceEqual("VP8X"u8) && b.Length >= 30)
        {
            int width = (b[24] | (b[25] << 8) | (b[26] << 16)) + 1;
            int height = (b[27] | (b[28] << 8) | (b[29] << 16)) + 1;
            return Valid(width, height);
        }

        return null;
    }

    // ── JPEG ─────────────────────────────────────────────────────────────────
    // 0xFFD8, then a chain of marker segments. The size lives in whichever
    // Start-Of-Frame segment comes first, which can sit behind any amount of
    // metadata — hence the walk rather than a fixed offset.
    private static (int, int)? ReadJpeg(ReadOnlySpan<byte> b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;

        int i = 2;

        // Four bytes is the smallest thing worth looking at: a marker plus its
        // length field. Whether a frame header's own body is present is checked
        // separately below, so a file truncated mid-header reports "unknown"
        // instead of reading past the end.
        while (i + 4 <= b.Length)
        {
            // Markers are 0xFF followed by a type byte; 0xFF padding between
            // segments is legal and skipped.
            if (b[i] != 0xFF) { i++; continue; }

            byte marker = b[i + 1];
            if (marker == 0xFF) { i++; continue; }

            // Standalone markers carry no length field.
            if (marker is 0x01 or >= 0xD0 and <= 0xD9) { i += 2; continue; }

            int length = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 2, 2));
            if (length < 2) return null;

            // SOF0-SOF15 hold the frame size. C4 (Huffman tables), C8 (JPEG
            // extensions) and CC (arithmetic coding conditioning) share the range
            // but are not frame headers.
            bool isStartOfFrame = marker >= 0xC0 && marker <= 0xCF
                && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

            if (isStartOfFrame)
            {
                // Segment body: 1 byte sample precision, then height and width as
                // big-endian 16-bit integers — height first.
                if (i + 9 > b.Length) return null;

                int height = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 5, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(i + 7, 2));
                return Valid(width, height);
            }

            i += 2 + length;
        }

        return null;
    }

    private static (int, int)? Valid(int width, int height) =>
        width > 0 && height > 0 ? (width, height) : null;
}
