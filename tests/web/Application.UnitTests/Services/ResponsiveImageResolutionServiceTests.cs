using Application.Services;
using Domain.Entities.PublicMedia;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using Shouldly;
using SharedKernel.Concrete;
using SharedKernel.Media;

namespace Application.UnitTests.Services;

/// <summary>
/// Covers the render-time upgrade of embedded images: <c>srcset</c> and a WebP
/// <c>&lt;picture&gt;</c> source from the renditions the CMS recorded on the image's
/// row, plus the <c>alt</c>, <c>width</c> and <c>height</c> the media library
/// already knows.
/// <para>
/// The behaviour that matters most here is restraint — the service must leave
/// anything the author decided for themselves alone, and must not touch images that
/// are not ours at all.
/// </para>
/// </summary>
public sealed class ResponsiveImageResolutionServiceTests
{
    private readonly DbContextOptions<PublicReadDbContext> _options = TestDbFactory.CreateOptions();

    public ResponsiveImageResolutionServiceTests()
    {
        using PublicReadDbContext db = TestDbFactory.Create(_options);
        db.MediaFiles.AddRange(
            // A JPEG master with the full ladder the CMS derives: WebP + JPEG at
            // each smaller width, WebP at the master's own size.
            new PublicMediaFile
            {
                Id = 1,
                FilePath = "/uploads/images/2026/09/photo.jpg",
                MimeType = "image/jpeg",
                AltText = "Göl kenarında orman",
                Width = 1920,
                Height = 1080,
                Renditions = ImageRenditions.ToJson(
                [
                    new ImageRendition(480, 270, "/uploads/images/2026/09/photo-480w.webp", "/uploads/images/2026/09/photo-480w.jpg"),
                    new ImageRendition(960, 540, "/uploads/images/2026/09/photo-960w.webp", "/uploads/images/2026/09/photo-960w.jpg"),
                    new ImageRendition(1440, 810, "/uploads/images/2026/09/photo-1440w.webp", "/uploads/images/2026/09/photo-1440w.jpg"),
                    new ImageRendition(1920, 1080, "/uploads/images/2026/09/photo-1920w.webp", null),
                ])
            },
            // A lone image: has alt text and dimensions, but no renditions (yet).
            new PublicMediaFile
            {
                Id = 4,
                FilePath = "/uploads/images/2026/09/solo.png",
                MimeType = "image/png",
                AltText = "Tek başına görsel",
                Width = 800,
                Height = 600
            },
            // A WebP master: WebP renditions only, no second format to fall back to.
            new PublicMediaFile
            {
                Id = 5,
                FilePath = "/uploads/images/2026/09/banner.webp",
                MimeType = "image/webp",
                Width = 1600,
                Height = 900,
                Renditions = ImageRenditions.ToJson(
                [
                    new ImageRendition(480, 270, "/uploads/images/2026/09/banner-480w.webp", null),
                    new ImageRendition(960, 540, "/uploads/images/2026/09/banner-960w.webp", null),
                ])
            });
        db.SaveChanges();
    }

    private async Task<string> ResolveAsync(string html, string? css = null)
    {
        await using PublicReadDbContext db = TestDbFactory.Create(_options);
        Result<string?> result = await new ResponsiveImageResolutionService(db)
            .ResolveAsync(html, css, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        return result.Value ?? "";
    }

    [Fact]
    public async Task Webp_renditions_go_into_a_picture_source_and_the_img_keeps_the_jpeg_fallback()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/photo.jpg">""");

        html.ShouldContain("<picture>");
        html.ShouldContain("""<source type="image/webp" srcset="/uploads/images/2026/09/photo-480w.webp 480w, /uploads/images/2026/09/photo-960w.webp 960w, /uploads/images/2026/09/photo-1440w.webp 1440w, /uploads/images/2026/09/photo-1920w.webp 1920w" """.TrimEnd());
        // The <img>'s own srcset is JPEG only, ending in the master itself — WebP
        // never lands where a browser that cannot read it would pick it.
        html.ShouldContain("""srcset="/uploads/images/2026/09/photo-480w.jpg 480w, /uploads/images/2026/09/photo-960w.jpg 960w, /uploads/images/2026/09/photo-1440w.jpg 1440w, /uploads/images/2026/09/photo.jpg 1920w" """.TrimEnd());
    }

    [Fact]
    public async Task A_webp_master_gets_a_picture_source_and_sizes_but_no_fallback_srcset()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/banner.webp">""");

        html.ShouldContain("""<source type="image/webp" srcset="/uploads/images/2026/09/banner-480w.webp 480w, /uploads/images/2026/09/banner-960w.webp 960w" sizes="(max-width: 1600px) 100vw, 1600px" """.TrimEnd());
        html.ShouldNotContain("""<img src="/uploads/images/2026/09/banner.webp" srcset""");
    }

    [Fact]
    public async Task A_pixel_width_in_the_pages_css_becomes_the_sizes_hint()
    {
        string html = await ResolveAsync(
            """<img id="ilogo" src="/uploads/images/2026/09/photo.jpg">""",
            "#ilogo{display:block;width:200px;height:auto}");

        html.ShouldContain("""sizes="200px" """.TrimEnd());
        html.ShouldNotContain("100vw");
    }

    /// <summary>
    /// The listing block stretches its card image to the full card below 767px, so
    /// "160px" alone sent phones a file half the size they needed.
    /// </summary>
    [Fact]
    public async Task A_page_listing_card_image_is_full_width_on_phones()
    {
        string html = await ResolveAsync(
            """<div class="elevare-page-listing"><div class="elevare-listing-items"><a><img id="icard" src="/uploads/images/2026/09/photo.jpg"></a></div></div>""",
            "#icard{width:160px;height:110px;object-fit:cover}");

        html.ShouldContain("""sizes="(max-width: 767px) 100vw, 160px" """.TrimEnd());
    }

    [Fact]
    public async Task An_image_without_renditions_is_served_as_before()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/solo.png">""");

        html.ShouldNotContain("<picture>");
        html.ShouldNotContain("srcset");
    }

    /// <summary>
    /// Every image the author marked eager is fetched at high priority — the
    /// server cannot see the layout, and picking one went wrong on the live site
    /// both ways (a header logo, a photo in a narrow column over the hero).
    /// </summary>
    [Fact]
    public async Task Every_eager_image_gets_fetchpriority_high()
    {
        string html = await ResolveAsync(
            """<img src="/uploads/images/2026/09/solo.png" loading="eager" sizes="66px"><img src="/uploads/images/2026/09/photo.jpg" loading="eager"><img src="/uploads/images/2026/09/photo.jpg" loading="lazy" id="below">""");

        (html.Length - html.Replace("fetchpriority=\"high\"", "").Length).ShouldBe(2 * "fetchpriority=\"high\"".Length);
        html.ShouldNotContain("id=\"below\" fetchpriority");
    }

    [Fact]
    public async Task An_authors_own_fetchpriority_is_left_alone()
    {
        string html = await ResolveAsync(
            """<img src="/uploads/images/2026/09/solo.png" loading="eager" fetchpriority="low">""");

        html.ShouldContain("fetchpriority=\"low\"");
        html.ShouldNotContain("fetchpriority=\"high\"");
    }

    /// <summary>
    /// Without a <c>sizes</c> hint the browser assumes the image is as wide as the
    /// viewport and always downloads the largest file — which would make the srcset
    /// worse than no srcset on a phone.
    /// </summary>
    [Fact]
    public async Task Emits_a_sizes_hint_capped_at_the_master_width()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/photo.jpg">""");

        html.ShouldContain("""sizes="(max-width: 1920px) 100vw, 1920px" """.TrimEnd());
    }

    [Fact]
    public async Task Fills_in_alt_text_and_intrinsic_size_from_the_library()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/solo.png">""");

        html.ShouldContain("""alt="Tek başına görsel" """.TrimEnd());
        html.ShouldContain("""width="800" """.TrimEnd());
        html.ShouldContain("""height="600" """.TrimEnd());
    }

    /// <summary>
    /// An empty alt is a decision — it marks a decorative image — and overwriting it
    /// would make a screen reader announce a picture the author deliberately hid.
    /// </summary>
    [Fact]
    public async Task Leaves_an_explicitly_empty_alt_alone()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/solo.png" alt="">""");

        html.ShouldNotContain("Tek başına görsel");
    }

    [Fact]
    public async Task Leaves_author_written_alt_text_alone()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/solo.png" alt="Yazarın kendi metni">""");

        html.ShouldContain("Yazarın kendi metni");
        html.ShouldNotContain("Tek başına görsel");
    }

    [Fact]
    public async Task Leaves_a_hand_written_srcset_alone()
    {
        string html = await ResolveAsync(
            """<img src="/uploads/images/2026/09/photo.jpg" srcset="/custom.jpg 100w">""");

        html.ShouldContain("/custom.jpg 100w");
        html.ShouldNotContain("photo-480w.jpg 480w");
    }

    /// <summary>
    /// Completing a half-specified pair would invent an aspect ratio the author did
    /// not ask for, stretching the image.
    /// </summary>
    [Fact]
    public async Task Does_not_complete_a_partially_specified_size()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/solo.png" width="200">""");

        html.ShouldNotContain("height=");
    }

    [Fact]
    public async Task Ignores_images_that_are_not_from_the_media_library()
    {
        const string source = """<img src="https://cdn.example.com/logo.png">""";
        string html = await ResolveAsync(source);

        html.ShouldNotContain("srcset");
        html.ShouldNotContain("alt=");
    }

    /// <summary>
    /// The same file is written into pages as a relative path today, but pages
    /// authored before that change hold an absolute URL on the CMS host. Both must
    /// resolve to the same media row, or those older pages would silently stop
    /// getting responsive images. Relative paths are the case to watch: on Linux
    /// <c>Uri.TryCreate(src, UriKind.Absolute, …)</c> accepts a bare filesystem path
    /// and hands back a "file" URI, which an earlier scheme check rejected — green
    /// on Windows, broken in the container. The check is by string prefix now.
    /// </summary>
    [Fact]
    public async Task Matches_the_same_file_through_an_absolute_url()
    {
        string html = await ResolveAsync(
            """<img src="https://cms.example.com/uploads/images/2026/09/photo.jpg">""");

        html.ShouldContain("photo-480w.jpg 480w");
    }

    [Fact]
    public async Task Matches_a_url_that_carries_a_query_string()
    {
        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/photo.jpg?v=3">""");

        html.ShouldContain("photo-480w.jpg 480w");
    }

    /// <summary>
    /// A deleted image is served as a plain <c>&lt;img&gt;</c> — its renditions go
    /// with it, because they are on its row, not rows of their own. (Deleted or not
    /// is the whole story for media; PublicReadDbContext's query filter enforces it.)
    /// </summary>
    [Fact]
    public async Task A_deleted_image_loses_its_renditions_with_it()
    {
        await using (PublicReadDbContext db = TestDbFactory.Create(_options))
        {
            // AsTracking is required: the context this projects is read-only by
            // design and turns tracking off globally (see PublicReadDbContext), so
            // an untracked edit here would silently save nothing.
            PublicMediaFile photo = await db.MediaFiles.AsTracking().SingleAsync(m => m.Id == 1);
            photo.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        string html = await ResolveAsync("""<img src="/uploads/images/2026/09/photo.jpg">""");

        html.ShouldNotContain("srcset");
        html.ShouldNotContain("<picture>");
    }

    [Fact]
    public async Task Leaves_html_without_uploads_untouched()
    {
        const string source = "<p>Hiç görsel yok</p>";
        string html = await ResolveAsync(source);

        html.ShouldBe(source);
    }
}
