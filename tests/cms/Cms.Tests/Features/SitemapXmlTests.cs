using Domain.Entities.PageInfos;
using Infrastructure.Sitemap;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// What the generated sitemaps declare about themselves.
/// <para>
/// XmlWriter takes the encoding for its declaration from the writer it is handed,
/// not from XmlWriterSettings. Built over a plain StringWriter it wrote
/// <c>encoding="utf-16"</c> — because .NET strings are UTF-16 — while the site
/// served the bytes as UTF-8. A declaration that contradicts the actual encoding is
/// something a strict parser may reject outright, and search engines are the strict
/// parsers that matter for a sitemap.
/// </para>
/// </summary>
public sealed class SitemapXmlTests
{
    [Fact]
    public void The_sitemap_index_declares_utf8()
    {
        string xml = SitemapJob.BuildSitemapIndexXml(
            [("https://example.com/sitemap-pages.xml", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc))]);

        xml.ShouldContain("encoding=\"utf-8\"");
        xml.ShouldNotContain("utf-16");
    }

    [Fact]
    public void A_url_set_declares_utf8()
    {
        PageInfo page = new()
        {
            Id = 1,
            Slug = "hakkimizda",
            FullSlug = "hakkimizda",
            PageStatus = PageStatus.Published,
            SeoMeta = new SeoMeta { Title = "Hakkımızda" },
        };

        string xml = SitemapJob.BuildUrlSetXml("https://example.com", [page], [page], "tr");

        xml.ShouldContain("encoding=\"utf-8\"");
        xml.ShouldNotContain("utf-16");
    }

    [Fact]
    public void The_index_still_lists_what_it_was_given()
    {
        // Guards against "fixing" the declaration by breaking the content.
        string xml = SitemapJob.BuildSitemapIndexXml(
            [("https://example.com/sitemap-pages.xml", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc))]);

        xml.ShouldContain("<loc>https://example.com/sitemap-pages.xml</loc>");
        xml.ShouldContain("<lastmod>2026-01-02</lastmod>");
    }
}
