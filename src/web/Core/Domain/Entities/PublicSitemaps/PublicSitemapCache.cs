namespace Domain.Entities.PublicSitemaps;

/// <summary>
/// Read-only projection of the CMS's <c>SitemapCaches</c> table. The CMS's
/// SitemapJob (Hangfire, every 2 hours) regenerates these XML blobs; the Web
/// app only ever serves them as-is via SitemapController.
/// </summary>
public sealed class PublicSitemapCache
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = null!;
    public string XmlContent { get; set; } = null!;
}
