using Domain.Entities.Media;
using Infrastructure.Files;
using SharedKernel.Media;
using Shouldly;
using SkiaSharp;

namespace Cms.Tests.Media;

/// <summary>
/// The image pipeline behind an upload: what is kept as the master, and what is
/// derived from it. The bytes come from SkiaSharp itself — a drawn bitmap encoded
/// on the spot — so the tests carry no fixtures and prove the real encoder path.
/// </summary>
public sealed class ImageDerivativesTests
{
    private static byte[] Encode(int width, int height, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            using var paint = new SKPaint { Color = SKColors.White };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality);
        return data.ToArray();
    }

    private static byte[] Jpeg(int width, int height, int quality = 90) => Encode(width, height, SKEncodedImageFormat.Jpeg, quality);
    private static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png, 100);

    private static (int Width, int Height) Measure(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        return (bitmap.Width, bitmap.Height);
    }

    // ── Master ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_master_over_the_cap_is_brought_down_to_2560_on_its_long_edge()
    {
        ImageDerivatives.Master? master = ImageDerivatives.BuildMaster(Jpeg(4000, 3000), "image/jpeg");

        master.ShouldNotBeNull();
        master.Reencoded.ShouldBeTrue();
        master.Width.ShouldBe(2560);
        master.Height.ShouldBe(1920);
        Measure(master.Bytes).ShouldBe((2560, 1920));
    }

    [Fact]
    public void A_portrait_master_is_capped_on_its_height()
    {
        ImageDerivatives.Master? master = ImageDerivatives.BuildMaster(Jpeg(3000, 4000), "image/jpeg");

        master.ShouldNotBeNull();
        master.Height.ShouldBe(2560);
        master.Width.ShouldBe(1920);
    }

    /// <summary>
    /// A JPEG within the cap is re-encoded at the master quality only when that is
    /// a real saving; one that is already lean is stored as the author made it.
    /// </summary>
    [Fact]
    public void A_heavy_jpeg_within_the_cap_is_recompressed_and_a_lean_one_is_kept()
    {
        byte[] heavy = Jpeg(1600, 1200, quality: 100);
        ImageDerivatives.Master? fromHeavy = ImageDerivatives.BuildMaster(heavy, "image/jpeg");
        fromHeavy.ShouldNotBeNull();
        fromHeavy.Reencoded.ShouldBeTrue();
        fromHeavy.Bytes.Length.ShouldBeLessThan(heavy.Length);
        (fromHeavy.Width, fromHeavy.Height).ShouldBe((1600, 1200));

        byte[] lean = Jpeg(1600, 1200, quality: 60);
        ImageDerivatives.Master? fromLean = ImageDerivatives.BuildMaster(lean, "image/jpeg");
        fromLean.ShouldNotBeNull();
        fromLean.Reencoded.ShouldBeFalse();
        fromLean.Bytes.ShouldBeSameAs(lean);
    }

    [Fact]
    public void A_png_within_the_cap_is_stored_exactly_as_uploaded()
    {
        byte[] png = Png(800, 600);
        ImageDerivatives.Master? master = ImageDerivatives.BuildMaster(png, "image/png");

        master.ShouldNotBeNull();
        master.Reencoded.ShouldBeFalse();
        master.Bytes.ShouldBeSameAs(png);
        (master.Width, master.Height).ShouldBe((800, 600));
    }

    [Fact]
    public void A_png_over_the_cap_stays_png()
    {
        ImageDerivatives.Master? master = ImageDerivatives.BuildMaster(Png(3000, 1000), "image/png");

        master.ShouldNotBeNull();
        master.Width.ShouldBe(2560);
        // PNG signature: the format is preserved, not turned into JPEG.
        master.Bytes.Take(8).ShouldBe([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    }

    [Fact]
    public void Nothing_is_made_of_a_gif_an_svg_or_garbage()
    {
        ImageDerivatives.CanDerive("image/gif").ShouldBeFalse();
        ImageDerivatives.CanDerive("image/svg+xml").ShouldBeFalse();
        ImageDerivatives.BuildMaster([1, 2, 3], "image/jpeg").ShouldBeNull();
        ImageDerivatives.Generate([1, 2, 3], "image/jpeg").Items.ShouldBeEmpty();
    }

    // ── Renditions ────────────────────────────────────────────────────────────

    [Fact]
    public void A_large_master_gets_webp_and_jpeg_at_each_smaller_width_plus_a_webp_at_its_own_size()
    {
        ImageDerivatives.DerivedSet set = ImageDerivatives.Generate(Jpeg(2560, 1397), "image/jpeg");
        set.SourceWidth.ShouldBe(2560);
        set.SourceHeight.ShouldBe(1397);
        IReadOnlyList<ImageDerivatives.Derived> derived = set.Items;

        derived.Where(d => d.ContentType == "image/webp" && !d.IsFullSize).Select(d => d.Width)
            .ShouldBe([480, 640, 960, 1440, 1920]);
        derived.Where(d => d.ContentType == "image/jpeg").Select(d => d.Width)
            .ShouldBe([480, 640, 960, 1440, 1920]);
        ImageDerivatives.Derived full = derived.Single(d => d.IsFullSize);
        full.ContentType.ShouldBe("image/webp");
        full.Width.ShouldBe(2560);
        // Aspect ratio survives the resize.
        derived.First(d => d.Width == 960).Height.ShouldBe(524);
        derived.ShouldAllBe(d => d.Bytes.Length > 0);
    }

    [Fact]
    public void A_small_master_gets_only_its_full_size_webp()
    {
        IReadOnlyList<ImageDerivatives.Derived> derived = ImageDerivatives.Generate(Jpeg(400, 300), "image/jpeg").Items;

        derived.Count.ShouldBe(1);
        derived[0].IsFullSize.ShouldBeTrue();
        derived[0].Width.ShouldBe(400);
    }

    [Fact]
    public void A_webp_master_gets_smaller_webps_and_no_copy_of_itself()
    {
        byte[] webp = Encode(1600, 900, SKEncodedImageFormat.Webp, 80);
        IReadOnlyList<ImageDerivatives.Derived> derived = ImageDerivatives.Generate(webp, "image/webp").Items;

        derived.Select(d => d.Width).ShouldBe([480, 640, 960, 1440]);
        derived.ShouldAllBe(d => d.ContentType == "image/webp" && !d.IsFullSize);
    }

    // ── Backfill ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Adding a width to the ladder must reach existing images — that is what
    /// the backfill's "needs renditions" test decides — without redoing images
    /// that are complete, or narrow ones that could never have had the rung.
    /// </summary>
    [Fact]
    public void Backfill_redoes_an_image_missing_a_rung_and_leaves_complete_or_narrow_ones()
    {
        static MediaFile Image(int width, params int[] have) => new()
        {
            FileName = "a.jpg", OriginalFileName = "a.jpg", FilePath = "/uploads/images/a.jpg", MimeType = "image/jpeg",
            MediaType = MediaType.Image, Width = width, Height = width / 2,
            Renditions = have.Length == 0 ? null : ImageRenditions.ToJson([.. have.Select(w => new ImageRendition(w, w / 2, "/x-" + w + ".webp", null))]),
        };

        ImageDerivativesBackfillJob.NeedsRenditions(Image(2560)).ShouldBeTrue();                                  // never touched
        ImageDerivativesBackfillJob.NeedsRenditions(Image(2560, 480, 960, 1440, 1920, 2560)).ShouldBeTrue();      // pre-640 ladder
        ImageDerivativesBackfillJob.NeedsRenditions(Image(2560, 480, 640, 960, 1440, 1920, 2560)).ShouldBeFalse();
        ImageDerivativesBackfillJob.NeedsRenditions(Image(600, 480, 600)).ShouldBeFalse();                       // 640 would not fit
    }
}
