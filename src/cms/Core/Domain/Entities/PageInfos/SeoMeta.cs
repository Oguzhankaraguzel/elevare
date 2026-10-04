using Microsoft.EntityFrameworkCore;

namespace Domain.Entities.PageInfos;

[Owned]
public sealed class SeoMeta
{
    // Core meta tags
    public bool IsCanonical { get; set; } = true;
    public string? CanonicalUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string MetaAuthor { get; set; } = string.Empty;

    /// <summary>Focus keyword used by the page builder's SEO scoring tool.</summary>
    public string? FocusKeyword { get; set; }

    // ── Crawler directives ────────────────────────────────────────────────────
    // Emitted as a single <meta name="robots"> tag by the public site. Kept as two
    // booleans rather than one free-text field because the pair is what people
    // actually reach for, and a typo in free text ("no-index") fails silently —
    // the page stays indexed and nobody finds out until it ranks.

    /// <summary>
    /// Asks search engines to keep this page out of their index. The page stays
    /// publicly reachable — this is the switch for a paid-ad landing page that must
    /// not also show up in organic results and split its own traffic.
    /// </summary>
    public bool NoIndex { get; set; }

    /// <summary>
    /// Asks search engines not to follow the links on this page, so no ranking
    /// signal passes through them. Independent of <see cref="NoIndex"/>: a page can
    /// be indexed with unfollowed links, or excluded from the index while its links
    /// still count.
    /// </summary>
    public bool NoFollow { get; set; }

    /// <summary>JSON-LD structured data (schema.org). Optional but recommended for rich results.</summary>
    public string? StructuredData { get; set; }

    // Open Graph
    public string OgTitle { get; set; } = string.Empty;
    public string OgDescription { get; set; } = string.Empty;
    /// <summary>Open Graph type (e.g., "article", "website", "product").</summary>
    public string OgType { get; set; } = "website";
    public string? OgImage { get; set; }
    public string? OgUrl { get; set; }

    // Twitter Card
    /// <summary>Twitter card type (e.g., "summary", "summary_large_image").</summary>
    public string? TwitterCard { get; set; }
    /// <summary>Twitter site handle (e.g., @YourSite).</summary>
    public string? TwitterSite { get; set; }

    /// <summary>
    /// Everything else a page says to link-preview readers — image dimensions and
    /// alt, article dates/authors/tags, profile, book, video and music properties,
    /// fb:app_id, X creator/overrides/player/app — as <c>SharedKernel.Social.SocialMeta</c>
    /// JSON. Null when nothing beyond the columns above was ever set.
    /// </summary>
    public string? SocialJson { get; set; }

    /// <summary>
    /// Last score (0-100) reported by the page builder's client-side SEO analyzer
    /// (<c>elevare-seo.js</c>), persisted on every real save — null until the page
    /// has been saved at least once after the analyzer has run.
    /// </summary>
    public int? SeoScore { get; set; }

    /// <summary>When <see cref="SeoScore"/> was last written.</summary>
    public DateTime? SeoScoreUpdatedAt { get; set; }
}
