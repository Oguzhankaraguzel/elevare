using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities.Abstractions;
using Domain.Entities.Languages;
using Domain.Entities.Tags;

namespace Domain.Entities.PageInfos;
public class PageInfo : BaseEntity
{
    [Length(maximumLength: 50, minimumLength: 1, ErrorMessage = "Please enter a slug between 3 and 50 characters.")]
    public required string Slug { get; set; }

    /// <summary>
    /// Pre-computed full URL path stored by the application layer whenever the slug or parent changes.
    /// Format: "{langCode}/{parentSlug}/.../slug" for non-default languages,
    ///         "{parentSlug}/.../slug" for the default language. The homepage's own
    ///         "home" segment — and that of every page under it — never appears: the
    ///         homepage itself computes to "" (default language) or "{langCode}"
    ///         (any other), since "home" is what "/" already means, not a real segment.
    /// Use <see cref="ComputeFullSlug"/> to (re)calculate the value when the graph is loaded.
    /// </summary>
    [MaxLength(1000)]
    public string FullSlug { get; set; } = string.Empty;

    public PageStatus PageStatus { get; set; } = PageStatus.Draft;

    /// <summary>
    /// The status a save asked for, held back because that save's content is still
    /// sitting in <see cref="PageContents.PageContent.PreviewGjsHtml"/> awaiting an
    /// active workflow's approval.
    /// <para>
    /// Without this, a page could report itself <see cref="PageStatus.Published"/>
    /// the instant an editor picks that option, even though the HTML actually being
    /// served is still the old (or, for a brand-new page, empty) content — the
    /// approval gate would protect the content but not the status badge next to it.
    /// <see cref="Commands.Workflows.DecideApproval.DecideApprovalCommandHandler"/>
    /// applies this to <see cref="PageStatus"/> at the same moment it promotes the
    /// staged HTML, so both flip together.
    /// </para>
    /// </summary>
    public PageStatus? PendingStatus { get; set; }

    /// <summary>
    /// When the page counts as published — what listings show and sort by, and what
    /// structured data and share tags report. The date an Article block states when
    /// the page has one (a post written in 2023 and moved here in 2026 is from 2023);
    /// otherwise the moment the page first went live. Null while it never has.
    /// Kept up to date by PagePublication on every save and status change.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// CMS-only classification of what this page is (landing page, article, …).
    /// Never rendered and never crawled — see <see cref="PageKind"/>.
    /// </summary>
    public PageKind Kind { get; set; } = PageKind.Unspecified;

    public SeoMeta SeoMeta { get; set; } = new();

    #region Foreign Keys
    public int? ParentPageId { get; set; }
    public int LanguageId { get; set; }

    /// <summary>
    /// Optional reference to a <see cref="PageGroup"/> that groups this page with its
    /// language alternates. All pages sharing the same PageGroupId are hreflang siblings.
    /// </summary>
    public int? PageGroupId { get; set; }
    #endregion

    #region Navigation Props
    [ForeignKey(nameof(ParentPageId))]
    public virtual PageInfo? ParentPage { get; set; }
    public virtual ICollection<PageInfo>? ChildPages { get; set; }

    [ForeignKey(nameof(LanguageId))]
    public virtual Language Language { get; set; }

    [ForeignKey(nameof(PageGroupId))]
    public virtual PageGroup? PageGroup { get; set; }

    /// <summary>GrapeJS body content for builder pages (1:1). Null for routed pages.</summary>
    public virtual PageContents.PageContent? Content { get; set; }

    public virtual ICollection<Tag>? Tags { get; set; }

    /// <summary>
    /// Site codes that must NOT run on this page. Every snippet runs on every page
    /// unless it is listed here — see <see cref="SiteCodeSnippets.PageInfoSiteCodeExclusion"/>.
    /// </summary>
    public virtual ICollection<SiteCodeSnippets.SiteCodeSnippet>? ExcludedSiteCodeSnippets { get; set; }
    #endregion

    /// <summary>
    /// Calculates and stores <see cref="FullSlug"/> by walking the parent chain.
    /// Must be called with <see cref="ParentPage"/> navigation fully loaded.
    /// </summary>
    /// <param name="defaultLanguageTwoLetterCode">
    /// The <see cref="Language.TwoLetterCode"/> of the site's default language
    /// (e.g., "tr"). When this page's language matches, the code is omitted from the slug.
    /// </param>
    public void ComputeFullSlug(string defaultLanguageTwoLetterCode)
    {
        var segments = new List<string> { Slug };

        PageInfo? current = ParentPage;
        while (current is not null)
        {
            segments.Add(current.Slug);
            current = current.ParentPage;
        }

        segments.Reverse();

        // The root ancestor's slug is "home" exactly when this page IS the homepage
        // or sits somewhere underneath it — the app already treats "home" as the one
        // reserved, always-top-level slug that renders at "/" (see PageController,
        // UpdatePageStatusCommandHandler's/DeletePageCommandHandler's guard against
        // touching it). "/" needs no segment to say "this is the homepage", so it —
        // and every page below it — never carries "home" as a literal path piece.
        if (segments.Count > 0 && string.Equals(segments[0], "home", StringComparison.OrdinalIgnoreCase))
            segments.RemoveAt(0);

        string path = string.Join("/", segments);

        bool isDefault = Language?.TwoLetterCode is not null &&
                         string.Equals(Language.TwoLetterCode, defaultLanguageTwoLetterCode,
                             StringComparison.OrdinalIgnoreCase);

        string prefixed = path.Length == 0 ? Language!.TwoLetterCode : $"{Language!.TwoLetterCode}/{path}";
        FullSlug = isDefault ? path : prefixed;
    }
}
