using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;

namespace Domain.Entities.ContentBulkEdits;

/// <summary>
/// One affected page within a <see cref="ContentBulkEdits.ContentBulkEdit"/> run —
/// holds the page's GrapeJS content exactly as it was *before* the replace, so the
/// run can restore it verbatim on revert.
/// </summary>
public class ContentBulkEditItem : BaseEntity
{
    public int ContentBulkEditId { get; set; }
    public ContentBulkEdit ContentBulkEdit { get; set; } = null!;

    public int PageInfoId { get; set; }
    public PageInfo? PageInfo { get; set; }

    // Snapshotted so the history view still reads sensibly if the page is later
    // renamed, re-slugged, or soft-deleted.
    public string PageTitleSnapshot { get; set; } = null!;
    public string PageFullSlugSnapshot { get; set; } = null!;

    public int MatchCount { get; set; }

    public string? OldGjsHtml { get; set; }
    public string? OldGjsCss { get; set; }
    public string? OldGjsData { get; set; }

    /// <summary>
    /// The page's structured data before a <see cref="ContentBulkEditKind.StructuredDataRefresh"/>
    /// run. Null on find/replace runs, which never touch it — that is also how
    /// revert knows to leave it alone.
    /// </summary>
    public string? OldStructuredData { get; set; }
}
