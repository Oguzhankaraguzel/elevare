using SkiaSharp;

namespace Infrastructure.Files;

/// <summary>
/// What the CMS makes of an uploaded image: the <b>master</b> it keeps, and the
/// smaller and lighter copies (<b>renditions</b>) the public site's <c>srcset</c> is
/// built from. Before this, an image was served at exactly the size and format it
/// was uploaded in — a 2816-pixel, 2 MB JPEG behind a 500-pixel logo, which is what
/// Lighthouse measured on the live homepage (3.7 MB of the page's 4 MB was two such
/// files).
/// <para>
/// <b>Master</b> (<see cref="BuildMaster"/>): the upload, capped at
/// <see cref="MaxLongEdge"/> on its long edge, with the camera's orientation tag
/// applied and the metadata behind it (GPS, device) dropped. Nothing on a web page
/// needs more pixels than that, so the raw upload is not kept; what is kept is
/// still the source every rendition is made from and can be remade from. A JPEG is
/// re-encoded at <see cref="MasterJpegQuality"/> — visually the same, a fraction of
/// the bytes; a PNG stays lossless; a WebP keeps its quality. An upload already
/// within the cap and correctly oriented is stored as it came, unless re-encoding
/// makes it clearly smaller.
/// </para>
/// <para>
/// <b>Renditions</b> (<see cref="Generate"/>): for each width in <see cref="Widths"/>
/// below the master's, a WebP (typically a quarter of the JPEG's bytes at the same
/// quality) and a copy in the master's own format — the fallback for the few readers
/// without WebP. Plus a WebP at the master's own size, so even the largest candidate
/// is the light one. GIF and SVG are left alone — a resized GIF loses its animation
/// and an SVG has no pixels to resize.
/// </para>
/// </summary>
internal static class ImageDerivatives
{
    /// <summary>
    /// Widths derived, in pixels; only those below the master's width are made.
    /// 640 sits between 480 and 960 for the phone case Lighthouse measured: a
    /// 335-CSS-pixel image on a 1.75× screen needs 586 device pixels, for which
    /// 480 is too small and 960 was 15 KB more than necessary.
    /// </summary>
    public static readonly int[] Widths = [480, 640, 960, 1440, 1920];

    /// <summary>Longest edge of the stored master; nothing on a web page needs more.</summary>
    public const int MaxLongEdge = 2560;

    /// <summary>The master JPEG's quality: no visible loss, a fraction of a camera original's bytes.</summary>
    public const int MasterJpegQuality = 90;

    private const int WebpQuality = 80;
    private const int JpegQuality = 82;

    // Below this saving a re-encode is not worth replacing bytes the author chose.
    private const double ReencodeKeepRatio = 0.9;

    public sealed record Derived(byte[] Bytes, int Width, int Height, string ContentType, string Extension, bool IsFullSize);

    /// <summary>The master's own pixel size and what was derived from it.</summary>
    public sealed record DerivedSet(int SourceWidth, int SourceHeight, IReadOnlyList<Derived> Items)
    {
        public static readonly DerivedSet Empty = new(0, 0, []);
    }

    /// <summary>
    /// The image to store. <see cref="Bytes"/> are the upload's own when nothing had
    /// to change (<see cref="Reencoded"/> false); the pixel size is always measured.
    /// </summary>
    public sealed record Master(byte[] Bytes, int Width, int Height, bool Reencoded);

    public static bool CanDerive(string contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/webp";

    /// <summary>
    /// Null when the bytes cannot be decoded — an upload that is not a readable
    /// image is still an upload; it is stored as it came and gets no renditions.
    /// </summary>
    public static Master? BuildMaster(byte[] original, string contentType)
    {
        if (!CanDerive(contentType)) return null;

        using var codec = SKCodec.Create(new MemoryStream(original, writable: false), out SKCodecResult probe);
        if (probe != SKCodecResult.Success) return null;
        SKEncodedOrigin origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0) return null;

        using SKBitmap upright = ApplyOrigin(decoded, origin);
        int longEdge = Math.Max(upright.Width, upright.Height);
        bool capped = longEdge > MaxLongEdge;
        bool rotated = origin != SKEncodedOrigin.TopLeft;

        (string _, string _, SKEncodedImageFormat format, int quality) = FallbackFormat(contentType);
        if (format == SKEncodedImageFormat.Jpeg) quality = MasterJpegQuality;

        if (!capped && !rotated)
        {
            // Already the right size and the right way up. A PNG or WebP is kept as
            // uploaded (re-encoding a lossless file gains nothing, a lossy one only
            // loses); a JPEG is tried at the master quality and the smaller wins —
            // a phone's 12 MP JPEG shrinks by two thirds, an already-optimised one
            // stays as the author made it.
            if (format != SKEncodedImageFormat.Jpeg) return new Master(original, upright.Width, upright.Height, Reencoded: false);
            Derived tried = Encode(upright, format, quality, "image/jpeg", ".jpg", isFullSize: true);
            return tried.Bytes.Length < original.Length * ReencodeKeepRatio
                ? new Master(tried.Bytes, upright.Width, upright.Height, Reencoded: true)
                : new Master(original, upright.Width, upright.Height, Reencoded: false);
        }

        int width = capped ? (int)Math.Round(upright.Width * (MaxLongEdge / (double)longEdge)) : upright.Width;
        using SKBitmap sized = width == upright.Width ? upright.Copy() : Resize(upright, width);
        Derived encoded = Encode(sized, format, quality, contentType, "", isFullSize: true);
        return new Master(encoded.Bytes, sized.Width, sized.Height, Reencoded: true);
    }

    /// <summary>
    /// Renditions of a master (see <see cref="BuildMaster"/>). Empty when the image
    /// cannot be decoded. Never throws for bad pixels.
    /// </summary>
    public static DerivedSet Generate(byte[] master, string contentType)
    {
        if (!CanDerive(contentType)) return DerivedSet.Empty;

        // Codec first: SKBitmap.Decode(bytes) throws on bytes no codec recognises
        // (it passes a null codec through), and "cannot be decoded" is an answer
        // here, not an error.
        using var codec = SKCodec.Create(new MemoryStream(master, writable: false), out SKCodecResult probe);
        if (probe != SKCodecResult.Success) return DerivedSet.Empty;
        using var source = SKBitmap.Decode(codec);
        if (source is null || source.Width <= 0 || source.Height <= 0) return DerivedSet.Empty;

        List<Derived> results = [];
        (string fallbackType, string fallbackExt, SKEncodedImageFormat fallbackFormat, int fallbackQuality) = FallbackFormat(contentType);

        foreach (int width in Widths)
        {
            if (width >= source.Width) break;
            using SKBitmap resized = Resize(source, width);
            results.Add(Encode(resized, SKEncodedImageFormat.Webp, WebpQuality, "image/webp", ".webp", isFullSize: false));
            // The master's own format at the same width is the fallback; a WebP
            // master needs no second copy of itself.
            if (fallbackFormat != SKEncodedImageFormat.Webp)
                results.Add(Encode(resized, fallbackFormat, fallbackQuality, fallbackType, fallbackExt, isFullSize: false));
        }

        // The master's own size as WebP, so the largest candidate is the light one.
        // A WebP master already is that file.
        if (contentType != "image/webp")
        {
            using SKBitmap full = source.Copy();
            results.Add(Encode(full, SKEncodedImageFormat.Webp, WebpQuality, "image/webp", ".webp", isFullSize: true));
        }

        return new DerivedSet(source.Width, source.Height, results);
    }

    private static (string ContentType, string Extension, SKEncodedImageFormat Format, int Quality) FallbackFormat(string contentType) =>
        contentType switch
        {
            "image/png" => ("image/png", ".png", SKEncodedImageFormat.Png, 100),
            "image/webp" => ("image/webp", ".webp", SKEncodedImageFormat.Webp, WebpQuality),
            _ => ("image/jpeg", ".jpg", SKEncodedImageFormat.Jpeg, JpegQuality),
        };

    // A phone stores the pixels as the sensor saw them and a tag saying "rotate
    // me"; browsers honour the tag, but a resize that ignores it would produce
    // sideways renditions of an upright original. The pixels are turned once here
    // and the tag is not carried over, so master and renditions agree.
    private static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bitmap.Copy();

        bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int w = swap ? bitmap.Height : bitmap.Width;
        int h = swap ? bitmap.Width : bitmap.Height;

        var target = new SKBitmap(new SKImageInfo(w, h, bitmap.ColorType, bitmap.AlphaType, bitmap.ColorSpace));
        using var canvas = new SKCanvas(target);
        // Transforms compose in call order: the last call is applied to the pixels
        // first. Names are EXIF's — RightTop is "rotate 90° clockwise", LeftTop is
        // the transpose (that rotation followed by a horizontal flip), and so on.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(w, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(w, h); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, h); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, h); canvas.RotateDegrees(-90); break;
            case SKEncodedOrigin.LeftTop: canvas.Translate(w, 0); canvas.Scale(-1, 1); canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(0, h); canvas.Scale(1, -1); canvas.Translate(w, 0); canvas.RotateDegrees(90); break;
        }
        canvas.DrawBitmap(bitmap, 0, 0);
        return target;
    }

    private static SKBitmap Resize(SKBitmap source, int width)
    {
        int height = Math.Max(1, (int)Math.Round(source.Height * (width / (double)source.Width)));
        var info = new SKImageInfo(width, height, source.ColorType, source.AlphaType, source.ColorSpace);
        // Mitchell keeps edges crisp when shrinking; plain linear blurs a logo.
        SKBitmap resized = source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return resized ?? throw new InvalidOperationException("Image could not be resized.");
    }

    private static Derived Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality, string contentType, string extension, bool isFullSize)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"Image could not be encoded as {format}.");
        return new Derived(data.ToArray(), bitmap.Width, bitmap.Height, contentType, extension, isFullSize);
    }
}
