using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Sitemaps;

/// <summary>
/// Caches a generated sitemap XML blob so it can be served directly without re-computation.
/// The Hangfire job <c>SitemapJob</c> refreshes these records every 2 hours.
///
/// Key naming convention — the Web app's SitemapController looks up these exact keys:
///   "index"  → /sitemap.xml        (the sitemap index listing sub-sitemaps)
///   "{slug}" → /sitemap-{slug}.xml (the bare top-level slug, e.g. "blog")
/// </summary>
public class SitemapCache
{
    public int Id { get; init; }

    /// <summary>Unique key identifying this sitemap entry (see naming convention in class summary).</summary>
    [MaxLength(500)]
    public required string CacheKey { get; set; }

    /// <summary>
    /// The full URL path that serves this sitemap file (e.g., "/sitemap-yazilim.xml").
    /// Used to build &lt;loc&gt; elements in the sitemap index.
    /// </summary>
    [MaxLength(1000)]
    public required string SitemapUrl { get; set; }

    /// <summary>The generated XML content of this sitemap or sitemap index file.</summary>
    public required string XmlContent { get; set; }

    /// <summary>UTC timestamp when the XML was last generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recently modified page included in this sitemap.</summary>
    public DateTime? LastModified { get; set; }

    /// <summary>Number of URL entries in this sitemap (informational).</summary>
    public int EntryCount { get; set; }
}
