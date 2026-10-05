using System.Text;
using Application.Features.Queries.Sitemaps.GetSitemapXml;
using Application.Features.Queries.SiteSettings.GetPublicSiteSettings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SharedKernel.Concrete;

namespace WebMvc.Controllers;

/// <summary>
/// Serves crawler-facing files: the sitemap XML the CMS's SitemapJob (Hangfire,
/// every 2 hours) pre-generates into the <c>SitemapCaches</c> table (this
/// controller only reads and returns it, it never generates anything itself),
/// plus robots.txt and llms.txt, both editable from the CMS under SEO.
/// </summary>
[Route("")]
public sealed class SitemapController(ISender sender) : BaseController
{
    // Sitemaps only change every 2 hours (the Hangfire job's interval); a short
    // cache still meaningfully cuts DB hits from repeat crawler requests.
    // Tagged with the pages so the CMS's cache clear drops them too — the sitemap
    // job asks for one when it has rebuilt them, see SitemapJob.
    [HttpGet("sitemap.xml")]
    [OutputCache(Duration = 600, Tags = [PageController.OutputCacheTag])]
    public Task<IActionResult> Index(CancellationToken cancellationToken)
        => ServeAsync("index", cancellationToken);

    // The cache key is the bare section slug — the same string SitemapJob writes.
    // This used to ask for "pages/{slug}", which no writer ever produced, so every
    // sub-sitemap the index pointed at answered 404 and the index was a list of dead
    // links. Keep the two in step: SitemapJob.UpsertAsync is the other half.
    // Served from the site root: a file under /{slug}/ may only list URLs below that
    // path, and a section sitemap also holds the section page and its translations.
    [HttpGet("sitemap-{slug}.xml")]
    [OutputCache(Duration = 600, Tags = [PageController.OutputCacheTag])]
    public async Task<IActionResult> Section(string slug, CancellationToken cancellationToken)
    {
        // "index" is the sitemap index's own key, served only as /sitemap.xml.
        if (string.Equals(slug, "index", StringComparison.OrdinalIgnoreCase))
            throw NotFoundResource("The sitemap index is served at /sitemap.xml.");

        return await ServeAsync(slug, cancellationToken);
    }

    [HttpGet("robots.txt")]
    [OutputCache(Duration = 3600)]
    public async Task<IActionResult> Robots(CancellationToken cancellationToken)
    {
        string sitemapUrl = $"{Request.Scheme}://{Request.Host}/sitemap.xml";
        string? authored = await ReadSettingAsync("Seo.RobotsTxt", cancellationToken);

        string body = string.IsNullOrWhiteSpace(authored)
            ? $"User-agent: *\nDisallow: /api/\n"
            : authored.ReplaceLineEndings("\n").TrimEnd() + "\n";

        // Appended rather than left to the author on purpose: a robots.txt without it
        // is still valid, so nothing would look broken — the sitemap would just quietly
        // stop being discovered. Skipped only when they already declared one, so an
        // author pointing crawlers at a different sitemap keeps that choice.
        // Blank line first: Sitemap is group-independent, and butting it against the
        // last User-agent block reads as if it belonged to that crawler alone.
        if (!DeclaresSitemap(body))
            body += $"\nSitemap: {sitemapUrl}\n";

        return Content(body, "text/plain", Encoding.UTF8);
    }

    /// <summary>
    /// The convention LLM crawlers read to learn what a site is, in place of guessing
    /// from the rendered pages. Empty setting means the site has not opted in, and a
    /// 404 says that more honestly than an empty file would.
    /// </summary>
    [HttpGet("llms.txt")]
    [OutputCache(Duration = 3600)]
    public async Task<IActionResult> Llms(CancellationToken cancellationToken)
    {
        string? authored = await ReadSettingAsync("Seo.LlmsTxt", cancellationToken);

        if (string.IsNullOrWhiteSpace(authored))
            return NotFound();

        return Content(authored.ReplaceLineEndings("\n").TrimEnd() + "\n", "text/markdown", Encoding.UTF8);
    }

    private static bool DeclaresSitemap(string body) =>
        body.Split('\n').Any(line => line.TrimStart().StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase));

    private async Task<string?> ReadSettingAsync(string key, CancellationToken cancellationToken)
    {
        Result<Dictionary<string, string?>> settings =
            await sender.Send(new GetPublicSiteSettingsQuery(), cancellationToken);

        return settings.IsSuccess ? settings.Value.GetValueOrDefault(key) : null;
    }

    private async Task<IActionResult> ServeAsync(string cacheKey, CancellationToken cancellationToken)
    {
        Result<string> result = await sender.Send(new GetSitemapXmlQuery(cacheKey), cancellationToken);

        if (result.IsFailure)
            throw NotFoundResource(result.Error.Description);

        return Content(result.Value, "application/xml");
    }
}
