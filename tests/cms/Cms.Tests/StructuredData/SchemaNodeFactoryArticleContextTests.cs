using System.Text.Json.Nodes;
using Application.Features.Commands.StructuredData;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Shouldly;

namespace Cms.Tests.StructuredData;

/// <summary>
/// Covers what the Article node takes from beyond the Article block itself — the
/// Author Bio block, the page's settings and the media library — so a draft comes
/// out complete without the editor typing anything a second time.
/// </summary>
public sealed class SchemaNodeFactoryArticleContextTests
{
    private const string BaseUrl = "https://example.com";
    private const string PageUrl = BaseUrl + "/blog/bitwise";
    private const string Cover = "/uploads/images/2026/09/cover.jpg";
    private const string Portrait = "/uploads/images/2026/09/ada.jpg";

    // As found on a real page: the name lost its marker when it was made bold on the
    // canvas, the day slipped outside <time>, and the attribute kept the block's default.
    private const string Article = $"""
        <article data-elevare-article>
          <header>
            <h1 data-elevare-article-headline>C# ile Bitwise Operatörler</h1>
            <p>Yazar: <font color="#18181b"><b>Ada Lovelace</b></font> · 13<time data-elevare-article-date datetime="2026-01-01"> Ağustos 2023</time></p>
          </header>
          <div data-elevare-article-body>
            <p>Açılış paragrafı.</p>
            <img src="data:image/png;base64,AAAA">
            <img src="{Cover}">
          </div>
        </article>
        """;

    private const string Bio = $"""
        <aside data-elevare-block="elevare-authorbio">
          <img src="{Portrait}" alt="">
          <div>
            <p>Yazar</p>
            <h3>Ada Lovelace</h3>
            <p>Uzman, 4+ Yıllık Deneyim</p>
            <p>Analitik makineler ve algoritmalar üzerine çalışan, yazılım tarihinin ilk programcısı.</p>
            <div><a href="https://www.linkedin.com/in/ada">LinkedIn</a><a href="https://x.com/ada">X</a><a href="#">Boş</a></div>
          </div>
        </aside>
        """;

    private static ArticleContext Context(string? metaDescription = null, string? shareImage = null) => new(
        BaseUrl, metaDescription, shareImage,
        DateModified: new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc),
        Language: "tr",
        Keywords: ["bitwise", "csharp"],
        Media: new Dictionary<string, MediaInfo>(StringComparer.Ordinal)
        {
            [Cover] = new(1200, 630, "image/jpeg", "Kapak"),
            [Portrait] = new(400, 400, "image/jpeg", null),
        });

    [Fact]
    public void The_published_date_is_the_one_the_byline_shows()
    {
        JsonObject node = SchemaNodeFactory.BuildArticle(Article, PageUrl, Context())!;

        node["datePublished"]!.GetValue<string>().ShouldStartWith("2023-08-13");
        node["dateModified"]!.GetValue<string>().ShouldStartWith("2026-09-06");
    }

    [Fact]
    public void The_author_is_read_off_the_byline_when_its_marker_is_gone()
    {
        JsonObject node = SchemaNodeFactory.BuildArticle(Article, PageUrl, Context())!;

        node["author"]!["name"]!.GetValue<string>().ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void An_author_bio_for_the_same_person_fills_in_the_rest_of_the_author()
    {
        JsonObject author = SchemaNodeFactory.BuildArticle(Article + Bio, PageUrl, Context())!["author"]!.AsObject();

        author["name"]!.GetValue<string>().ShouldBe("Ada Lovelace");
        author["jobTitle"]!.GetValue<string>().ShouldBe("Uzman, 4+ Yıllık Deneyim");
        author["description"]!.GetValue<string>().ShouldStartWith("Analitik makineler");
        author["image"]!["url"]!.GetValue<string>().ShouldBe(BaseUrl + Portrait);
        author["image"]!["width"]!.GetValue<int>().ShouldBe(400);
        author["sameAs"]!.AsArray().Select(s => s!.GetValue<string>())
            .ShouldBe(["https://www.linkedin.com/in/ada", "https://x.com/ada"]);
    }

    [Fact]
    public void The_bio_blocks_marked_parts_win_over_its_shape()
    {
        string bio = """
            <aside data-elevare-author>
              <h3 data-elevare-author-name>Ada Lovelace</h3>
              <p data-elevare-author-bio>Kısa ama işaretli tanıtım.</p>
              <p data-elevare-author-role>Matematikçi</p>
            </aside>
            """;

        JsonObject author = SchemaNodeFactory.BuildArticle(Article + bio, PageUrl, Context())!["author"]!.AsObject();

        author["jobTitle"]!.GetValue<string>().ShouldBe("Matematikçi");
        author["description"]!.GetValue<string>().ShouldBe("Kısa ama işaretli tanıtım.");
    }

    [Fact]
    public void A_bio_about_someone_else_is_not_taken_for_the_articles_author()
    {
        string other = Bio.Replace("<h3>Ada Lovelace</h3>", "<h3>Grace Hopper</h3>", StringComparison.Ordinal);

        JsonObject author = SchemaNodeFactory.BuildArticle(Article + other, PageUrl, Context())!["author"]!.AsObject();

        author["name"]!.GetValue<string>().ShouldBe("Ada Lovelace");
        author.ContainsKey("jobTitle").ShouldBeFalse();
    }

    [Fact]
    public void The_image_is_the_articles_first_real_one_with_its_size_from_the_media_library()
    {
        JsonObject image = SchemaNodeFactory.BuildArticle(Article, PageUrl, Context(shareImage: "/paylasim.jpg"))!["image"]!.AsObject();

        image["url"]!.GetValue<string>().ShouldBe(BaseUrl + Cover);
        image["width"]!.GetValue<int>().ShouldBe(1200);
        image["height"]!.GetValue<int>().ShouldBe(630);
        image["encodingFormat"]!.GetValue<string>().ShouldBe("image/jpeg");
    }

    [Fact]
    public void Without_an_image_of_its_own_the_article_uses_the_share_image()
    {
        string plain = Article.Replace($"<img src=\"{Cover}\">", "", StringComparison.Ordinal);

        JsonObject node = SchemaNodeFactory.BuildArticle(plain, PageUrl, Context(shareImage: "/paylasim.jpg"))!;

        node["image"]!["url"]!.GetValue<string>().ShouldBe(BaseUrl + "/paylasim.jpg");
        node["image"]!.AsObject().ContainsKey("width").ShouldBeFalse();
    }

    [Fact]
    public void Page_settings_fill_description_publisher_language_and_keywords()
    {
        JsonObject node = SchemaNodeFactory.BuildArticle(Article, PageUrl, Context(metaDescription: "Sayfanın açıklaması."))!;

        node["description"]!.GetValue<string>().ShouldBe("Sayfanın açıklaması.");
        node["publisher"]![Domain.Entities.StructuredData.SchemaGraph.IdKey]!.GetValue<string>().ShouldBe(BaseUrl);
        node["mainEntityOfPage"]![Domain.Entities.StructuredData.SchemaGraph.IdKey]!.GetValue<string>().ShouldBe(PageUrl);
        node["inLanguage"]!.GetValue<string>().ShouldBe("tr");
        node["keywords"]!.GetValue<string>().ShouldBe("bitwise, csharp");
    }

    [Fact]
    public void Headlines_are_cut_to_what_google_shows()
    {
        string longOne = Article.Replace("C# ile Bitwise Operatörler", new string('a', 150), StringComparison.Ordinal);

        SchemaNodeFactory.BuildArticle(longOne, PageUrl, Context())!["headline"]!.GetValue<string>().Length.ShouldBe(110);
    }

    [Fact]
    public void A_page_with_a_bio_but_no_article_names_its_author_on_the_web_page()
    {
        var page = new PageInfo
        {
            Slug = "hakkimda", FullSlug = "hakkimda", LanguageId = 1,
            Content = new PageContent { GjsHtml = Bio },
        };

        JsonObject node = SchemaNodeFactory.BuildWebPage(page, BaseUrl, Context());

        node["author"]!["name"]!.GetValue<string>().ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void The_share_image_follows_the_same_chain_as_og_image()
    {
        var settings = new Dictionary<string, string?> { ["Seo.DefaultOgImageUrl"] = "/varsayilan.jpg" };
        var page = new PageInfo
        {
            Slug = "x", FullSlug = "x", LanguageId = 1,
            Content = new PageContent { GjsHtml = $"""<div class="elevare-tpl-ref"><img src="/logo.png"></div><img src="{Cover}">""" },
        };

        SchemaNodeFactory.ShareImage(page, settings).ShouldBe(Cover);
        page.SeoMeta.OgImage = "/kendi.jpg";
        SchemaNodeFactory.ShareImage(page, settings).ShouldBe("/kendi.jpg");
        page.SeoMeta.OgImage = null;
        page.Content.GjsHtml = "<p>Görselsiz</p>";
        SchemaNodeFactory.ShareImage(page, settings).ShouldBe("/varsayilan.jpg");
    }
}
