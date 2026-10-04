using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Domain.Entities.Sitemaps;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Xml;

namespace Infrastructure.Sitemap;

/// <summary>
/// Hangfire job that regenerates sitemap XML every 2 hours.
/// Registered as a recurring job with key "sitemap-generation".
/// </summary>
public sealed class SitemapJob
{
    /// <summary>
    /// The sitemap holding the homepages and every listed page no section claims
    /// (a top-level page outside the homepage, a translation with no counterpart in
    /// the default language). Also where a section whose slug would collide with this
    /// key or with the index ends up.
    /// </summary>
    internal const string MainKey = "main";

    private const string IndexKey = "index";
    private const string HomeSlug = "home";

    private readonly ICmsApplicationDbContext _db;
    private readonly IPublicSiteCacheInvalidator _publicSite;
    private readonly ILogger<SitemapJob> _logger;

    public SitemapJob(ICmsApplicationDbContext db, IPublicSiteCacheInvalidator publicSite, ILogger<SitemapJob> logger)
    {
        _db = db;
        _publicSite = publicSite;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sitemap generation started.");

        string baseUrl = await GetBaseUrlAsync(cancellationToken);
        string defaultLangCode = await GetDefaultLanguageCodeAsync(cancellationToken);

        // Every page, listed or not: a draft page still holds its children in the
        // tree, and its translations still belong to its section. Which of them get
        // listed is IsListed's call.
        List<PageInfo> pages = await _db.PageInfos
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Include(p => p.Language)
            .OrderBy(p => p.FullSlug)
            .ToListAsync(cancellationToken);

        List<(string Key, List<PageInfo> Pages)> sitemaps = GroupIntoSitemaps(pages, defaultLangCode);

        var subSitemapEntries = new List<(string url, DateTime lastMod)>();
        foreach ((string key, List<PageInfo> sitemapPages) in sitemaps)
        {
            string sitemapUrl = $"/{key}/sitemap.xml";
            DateTime lastMod = sitemapPages.Max(LastModified);
            string xml = BuildUrlSetXml(baseUrl, sitemapPages, pages, defaultLangCode);

            await UpsertAsync(key, sitemapUrl, xml, lastMod, sitemapPages.Count, cancellationToken);
            subSitemapEntries.Add(($"{baseUrl.TrimEnd('/')}{sitemapUrl}", lastMod));
        }

        List<PageInfo> listed = [.. pages.Where(IsListed)];
        DateTime indexLastMod = listed.Count > 0 ? listed.Max(LastModified) : DateTime.UtcNow;
        string indexXml = BuildSitemapIndexXml(subSitemapEntries);
        await UpsertAsync(IndexKey, "/sitemap.xml", indexXml, indexLastMod, subSitemapEntries.Count, cancellationToken);
        await RemoveStaleAsync([.. sitemaps.Select(s => s.Key)], cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        // The site keeps serving the sitemaps it has cached until told otherwise.
        _publicSite.RequestInvalidation();
        _logger.LogInformation("Sitemap generation completed. {Count} sub-sitemaps.", subSitemapEntries.Count);
    }

    /// <summary>
    /// A page the site serves and wants indexed. System pages are out: a sitemap is a
    /// list of pages worth indexing, and 404/500/maintenance are what the site shows
    /// when something has gone wrong. noindex pages go too — listing a page says
    /// "please index this" while its own meta tag says the opposite. A page in a
    /// language the site has not released is not served at all.
    /// <para>
    /// The parent's status is deliberately not consulted: the site serves a published
    /// page at its address even when an ancestor is a draft, so the sitemap lists it.
    /// </para>
    /// </summary>
    internal static bool IsListed(PageInfo page) =>
        page is { IsActive: true, IsDeleted: false, PageStatus: PageStatus.Published, SeoMeta.NoIndex: false }
        && page.Language is { IsActive: true, IsPublished: true, IsDeleted: false }
        && !SystemPageSlugs.All.Contains(page.Slug);

    /// <summary>
    /// Splits the listed pages into sub-sitemaps the way the site is organised: one
    /// per section — a page directly under the default language's homepage, or a
    /// top-level page beside it — holding the section, everything below it, and every
    /// language version of those pages; and <see cref="MainKey"/> for the homepages
    /// and whatever no section claimed. Each language version gets its own entry: a
    /// version named only as another page's alternate is never submitted itself.
    /// </summary>
    internal static List<(string Key, List<PageInfo> Pages)> GroupIntoSitemaps(IReadOnlyList<PageInfo> pages, string defaultLangCode)
    {
        bool IsDefaultLanguage(PageInfo p) =>
            string.Equals(p.Language?.TwoLetterCode, defaultLangCode, StringComparison.OrdinalIgnoreCase);
        static bool IsHomepage(PageInfo p) =>
            p.ParentPageId is null && string.Equals(p.Slug, HomeSlug, StringComparison.OrdinalIgnoreCase);

        PageInfo? home = pages.FirstOrDefault(p => IsHomepage(p) && IsDefaultLanguage(p));
        List<PageInfo> sections = [.. pages.Where(p => IsDefaultLanguage(p) && !IsHomepage(p)
            && (p.ParentPageId is null || p.ParentPageId == home?.Id))];

        ILookup<int, PageInfo> children = pages.Where(p => p.ParentPageId is not null).ToLookup(p => p.ParentPageId!.Value);
        ILookup<int, PageInfo> versions = pages.Where(p => p.PageGroupId is not null).ToLookup(p => p.PageGroupId!.Value);

        HashSet<int> claimed = [];
        var sitemaps = new Dictionary<string, List<PageInfo>>(StringComparer.OrdinalIgnoreCase);

        void Claim(string key, PageInfo page)
        {
            IEnumerable<PageInfo> withVersions = page.PageGroupId is int group ? versions[group] : [page];
            foreach (PageInfo version in withVersions.OrderBy(v => IsDefaultLanguage(v) ? 0 : 1).ThenBy(v => v.FullSlug))
            {
                if (!IsListed(version) || !claimed.Add(version.Id))
                    continue;
                if (!sitemaps.TryGetValue(key, out List<PageInfo>? list))
                    sitemaps[key] = list = [];
                list.Add(version);
            }
        }

        foreach (PageInfo homepage in pages.Where(IsHomepage).OrderBy(p => IsDefaultLanguage(p) ? 0 : 1))
            Claim(MainKey, homepage);

        foreach (PageInfo section in sections)
        {
            string key = section.Slug is MainKey or IndexKey ? MainKey : section.Slug;

            // Breadth-first over the whole subtree, drafts included, so a published
            // page under a draft parent still lands in its section.
            Queue<PageInfo> queue = new([section]);
            HashSet<int> seen = [section.Id];
            while (queue.TryDequeue(out PageInfo? page))
            {
                Claim(key, page);
                foreach (PageInfo child in children[page.Id].OrderBy(c => c.FullSlug))
                    if (seen.Add(child.Id))
                        queue.Enqueue(child);
            }
        }

        foreach (PageInfo page in pages)
            Claim(MainKey, page);

        // The main sitemap first, then the sections in the order they were found.
        return [.. sitemaps
            .OrderBy(s => s.Key == MainKey ? 0 : 1)
            .Select(s => (s.Key, s.Value))];
    }

    internal static string BuildSitemapIndexXml(List<(string url, DateTime lastMod)> entries)
    {
        StringBuilder sb = new();
        using var writer = XmlWriter.Create(new Utf8StringWriter(sb), new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("sitemapindex", "http://www.sitemaps.org/schemas/sitemap/0.9");
        foreach ((string url, DateTime lastMod) in entries)
        {
            writer.WriteStartElement("sitemap");
            writer.WriteElementString("loc", url);
            writer.WriteElementString("lastmod", lastMod.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }

    /// <param name="pages">The entries of this sitemap.</param>
    /// <param name="allPages">Every page, for finding each entry's language versions.</param>
    internal static string BuildUrlSetXml(string baseUrl, IReadOnlyList<PageInfo> pages, IReadOnlyList<PageInfo> allPages, string defaultLangCode)
    {
        ILookup<int, PageInfo> listedVersions = allPages
            .Where(p => p.PageGroupId is not null && IsListed(p))
            .ToLookup(p => p.PageGroupId!.Value);

        StringBuilder sb = new();
        using var writer = XmlWriter.Create(new Utf8StringWriter(sb), new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
        writer.WriteAttributeString("xmlns", "xhtml", null, "http://www.w3.org/1999/xhtml");
        string trimmedBase = baseUrl.TrimEnd('/');
        // "" is the default language's homepage now that "home" is never a literal
        // segment (see PageInfo.ComputeFullSlug) — a legitimate URL, not an unset value.
        string UrlOf(PageInfo p) => $"{trimmedBase}/{p.FullSlug.TrimStart('/')}";

        foreach (PageInfo page in pages)
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", UrlOf(page));
            writer.WriteElementString("lastmod", LastModified(page).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

            // Every version lists the whole set, itself included — Google ignores an
            // annotation the other side does not confirm. Only versions the site serves
            // and wants indexed: an alternate that 404s is a hreflang error. A page with
            // no other live version gets none; the annotation would say nothing.
            bool IsDefault(PageInfo v) => string.Equals(v.Language!.TwoLetterCode, defaultLangCode, StringComparison.OrdinalIgnoreCase);
            List<PageInfo> alternates = page.PageGroupId is int group
                ? [.. listedVersions[group].OrderBy(v => IsDefault(v) ? 0 : 1).ThenBy(v => v.Language!.TwoLetterCode)]
                : [];
            if (alternates.Count > 1)
            {
                foreach (PageInfo alt in alternates)
                    WriteAlternate(writer, alt.Language!.TwoLetterCode, UrlOf(alt));

                // Same rule as the page head: visitors whose language the site does not
                // have get the default language's version.
                PageInfo? fallback = alternates.Find(IsDefault);
                if (fallback is not null)
                    WriteAlternate(writer, "x-default", UrlOf(fallback));
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }

    private static void WriteAlternate(XmlWriter writer, string hreflang, string href)
    {
        writer.WriteStartElement("xhtml", "link", "http://www.w3.org/1999/xhtml");
        writer.WriteAttributeString("rel", "alternate");
        writer.WriteAttributeString("hreflang", hreflang);
        writer.WriteAttributeString("href", href);
        writer.WriteEndElement();
    }

    private static DateTime LastModified(PageInfo page) => page.UpdateDate ?? page.CreateDate;

    /// <summary>
    /// Every URL in every sitemap is built from this. It used to read a "SiteUrl" key
    /// that the settings catalogue has never contained, so the lookup always missed and
    /// every sitemap was silently published against <c>https://localhost</c> — valid
    /// XML, completely useless to a crawler. The real key is Advanced.PublicSiteBaseUrl,
    /// the same one preview links and llms.txt use.
    /// </summary>
    private async Task<string> GetBaseUrlAsync(CancellationToken ct)
    {
        SiteSetting? setting = await _db.SiteSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "Advanced.PublicSiteBaseUrl" && !s.IsDeleted, ct);

        if (string.IsNullOrWhiteSpace(setting?.Value))
        {
            _logger.LogWarning(
                "Advanced.PublicSiteBaseUrl is not set — sitemap URLs will point at localhost "
                + "and search engines will not be able to use them.");

            return "https://localhost";
        }

        return setting.Value;
    }

    private async Task<string> GetDefaultLanguageCodeAsync(CancellationToken ct)
    {
        Language? lang = await _db.Languages.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IsDefault && !l.IsDeleted, ct);
        return lang?.TwoLetterCode ?? "tr";
    }

    /// <summary>
    /// Drops cached sitemaps that were not written this run — a section unpublished,
    /// deleted, renamed, or emptied. Upserting alone never removes anything, so
    /// without this a section's sitemap keeps being served at its old URL long after
    /// the section is gone, listing pages that now 404.
    /// </summary>
    private async Task RemoveStaleAsync(List<string> written, CancellationToken ct)
    {
        HashSet<string> live = [.. written, IndexKey];

        List<SitemapCache> stale = await _db.SitemapCaches
            .Where(s => !live.Contains(s.CacheKey))
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        _db.SitemapCaches.RemoveRange(stale);
        _logger.LogInformation("Removed {Count} stale sitemap cache entries.", stale.Count);
    }

    private async Task UpsertAsync(string key, string url, string xml, DateTime? lastMod, int count, CancellationToken ct)
    {
        SitemapCache? existing = await _db.SitemapCaches.FirstOrDefaultAsync(s => s.CacheKey == key, ct);
        if (existing is null)
        {
            _db.SitemapCaches.Add(new SitemapCache
            {
                CacheKey = key, SitemapUrl = url, XmlContent = xml,
                GeneratedAt = DateTime.UtcNow, LastModified = lastMod, EntryCount = count
            });
        }
        else
        {
            existing.XmlContent = xml; existing.SitemapUrl = url;
            existing.GeneratedAt = DateTime.UtcNow; existing.LastModified = lastMod; existing.EntryCount = count;
        }
    }

    /// <summary>
    /// A StringWriter that reports UTF-8.
    /// <para>
    /// XmlWriter takes the encoding for its declaration from the writer it is given,
    /// not from XmlWriterSettings — and a plain StringWriter is UTF-16, because .NET
    /// strings are. The sitemaps therefore went out declaring
    /// <c>encoding="utf-16"</c> while being served as UTF-8 bytes, which is a
    /// contradiction a strict parser is entitled to reject. Search engines are
    /// exactly the strict parsers that matter here.
    /// </para>
    /// </summary>
    private sealed class Utf8StringWriter(StringBuilder builder)
        // Invariant culture, not the machine's: a sitemap is a machine-readable
        // document, and a Turkish locale would otherwise be free to format numbers
        // and dates in it the way a Turkish reader expects.
        : StringWriter(builder, System.Globalization.CultureInfo.InvariantCulture)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
