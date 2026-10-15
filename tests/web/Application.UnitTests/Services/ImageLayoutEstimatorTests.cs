using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Application.Services;
using Domain.Entities.PublicMedia;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using SharedKernel.Concrete;
using SharedKernel.Media;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// The <c>sizes</c> hint for images whose width comes from their grid and boxes —
/// on the live home page a card a third of the page wide was downloading the
/// 1920-pixel file meant for a full-width hero.
/// </summary>
public sealed class ImageLayoutEstimatorTests
{
    private static string? Estimate(string html, string css)
    {
        using IHtmlDocument document = new HtmlParser().ParseDocument(html);
        IElement image = document.QuerySelector("img")!;
        return new ImageLayoutEstimator(css).EstimateSizes(image);
    }

    [Fact]
    public void A_card_in_a_three_column_grid_inside_a_capped_box_is_a_third_of_the_cap()
    {
        string? sizes = Estimate(
            """<div class="inner"><ul class="cards"><li class="card"><img class="card-img"></li></ul></div>""",
            ".inner{max-width:1200px;margin:0 auto}.cards{display:grid !important;grid-template-columns:repeat(3,1fr);gap:24px}" +
            ".card-img{width:100%}@media (max-width: 767px){.cards{grid-template-columns:1fr}}");

        sizes.ShouldBe("(max-width: 767px) 100vw, 400px");
    }

    [Fact]
    public void An_auto_fit_grid_fits_as_many_minimum_columns_as_its_box_allows()
    {
        // The card grid block's own rule, which outranks a class asking for three columns.
        string css = ".inner{max-width:1200px}.cards{display:grid !important;grid-template-columns:repeat(3,1fr)}" +
            "#grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,367px),1fr));gap:24px}";

        Estimate("""<div class="inner"><ul id="grid" class="cards"><li><img></li></ul></div>""", css)
            .ShouldBe("(max-width: 767px) 100vw, 400px");
        // With nothing capping it, a column is under two minimums wide.
        Estimate("""<ul id="grid"><li><img></li></ul>""", css)
            .ShouldBe("(max-width: 767px) 100vw, 734px");
    }

    [Fact]
    public void A_pixel_column_is_the_images_width()
    {
        string? sizes = Estimate(
            """<a class="media"><img class="media-img"><div class="body"></div></a>""",
            ".media{display:grid;grid-template-columns:320px 1fr}");

        sizes.ShouldBe("(max-width: 767px) 100vw, 320px");
    }

    [Fact]
    public void A_grid_with_no_pixel_width_above_it_is_a_share_of_the_viewport()
    {
        string? sizes = Estimate(
            """<div class="row"><div><img></div><div></div></div>""",
            ".row{display:grid;grid-template-columns:1fr 1fr}");

        sizes.ShouldBe("(max-width: 767px) 100vw, 50vw");
    }

    [Fact]
    public void A_capped_box_alone_is_full_width_up_to_its_cap()
    {
        string? sizes = Estimate("""<section id="hero"><img></section>""", "#hero{max-width:900px}");

        sizes.ShouldBe("(max-width: 900px) 100vw, 900px");
    }

    [Fact]
    public void Nothing_known_says_nothing()
    {
        Estimate("""<div class="x"><img></div>""", ".x{color:red}").ShouldBeNull();
        Estimate("""<div class="x"><img></div>""", "").ShouldBeNull();
    }

    [Fact]
    public async Task A_lazy_image_lets_the_browser_measure_it_and_keeps_the_estimate_after()
    {
        DbContextOptions<PublicReadDbContext> options = TestDbFactory.CreateOptions();
        using (PublicReadDbContext seed = TestDbFactory.Create(options))
        {
            seed.MediaFiles.Add(new PublicMediaFile
            {
                Id = 1, FilePath = "/uploads/images/2026/10/card.jpg", MimeType = "image/jpeg", Width = 1920, Height = 1080,
                Renditions = ImageRenditions.ToJson(
                [
                    new ImageRendition(480, 270, "/uploads/images/2026/10/card-480w.webp", "/uploads/images/2026/10/card-480w.jpg"),
                    new ImageRendition(960, 540, "/uploads/images/2026/10/card-960w.webp", "/uploads/images/2026/10/card-960w.jpg"),
                ]),
            });
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        using PublicReadDbContext db = TestDbFactory.Create(options);
        Result<string?> result = await new ResponsiveImageResolutionService(db).ResolveAsync(
            """<ul class="cards"><li><img src="/uploads/images/2026/10/card.jpg" loading="lazy"></li></ul><img src="/uploads/images/2026/10/card.jpg" loading="eager">""",
            ".cards{display:grid;grid-template-columns:repeat(3,1fr)}",
            CancellationToken.None);

        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("""sizes="auto, (max-width: 767px) 100vw, 34vw" """.TrimEnd());
        // An eager image loads before layout: "auto" means nothing there.
        result.Value.ShouldContain("""sizes="(max-width: 1920px) 100vw, 1920px" """.TrimEnd());
    }
}
