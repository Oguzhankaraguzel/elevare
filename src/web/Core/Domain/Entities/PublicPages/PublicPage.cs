namespace Domain.Entities.PublicPages;

/// <summary>
/// Read-only projection of the CMS's <c>PageInfos</c> table (+ owned <c>SeoMeta</c>
/// columns), scoped to exactly what the public site needs to render a page. This
/// type is intentionally independent from the CMS's own <c>PageInfo</c> entity —
/// the Web app only ever reads this table, never writes it.
/// </summary>
public sealed class PublicPage
{
    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string FullSlug { get; set; } = null!;
    public int LanguageId { get; set; }
    public int? PageGroupId { get; set; }
    public int? ParentPageId { get; set; }
    public PublicPageStatus PageStatus { get; set; }
    /// <summary>What the page is (article, category, …) — the CMS's PageInfo.Kind.</summary>
    public PublicPageKind Kind { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? UpdateDate { get; set; }
    /// <summary>When the page counts as published — the CMS's PageInfo.PublishedAt. Null for pages it has not backfilled yet.</summary>
    public DateTime? PublishedAt { get; set; }

    // Owned SeoMeta columns (flattened; see CMS's PageInfoConfiguration for column names).
    public bool SeoIsCanonical { get; set; }
    public string? SeoCanonicalUrl { get; set; }
    public string SeoTitle { get; set; } = "";
    public string SeoMetaDescription { get; set; } = "";
    public string SeoMetaAuthor { get; set; } = "";
    /// <summary>Renders as <c>&lt;meta name="robots" content="noindex"&gt;</c> — see Index.cshtml.</summary>
    public bool SeoNoIndex { get; set; }
    /// <summary>Renders as the <c>nofollow</c> half of the same robots tag.</summary>
    public bool SeoNoFollow { get; set; }
    public string? SeoStructuredData { get; set; }
    public string OgTitle { get; set; } = "";
    public string OgDescription { get; set; } = "";
    public string OgType { get; set; } = "website";
    public string? OgImage { get; set; }
    public string? OgUrl { get; set; }
    public string? TwitterCard { get; set; }
    public string? TwitterSite { get; set; }
    /// <summary>The extended social metadata document — see <c>SharedKernel.Social.SocialMeta</c>.</summary>
    public string? SeoSocialJson { get; set; }

    public PublicPageContent? Content { get; set; }
}
