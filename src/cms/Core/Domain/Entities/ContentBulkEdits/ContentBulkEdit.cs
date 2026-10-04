using Domain.Entities.Abstractions;

namespace Domain.Entities.ContentBulkEdits;

/// <summary>
/// One "find text X across all pages, replace with Y" run — the header record for
/// a batch of <see cref="ContentBulkEditItem"/> snapshots, which is what lets the
/// whole run be reverted later.
/// </summary>
public class ContentBulkEdit : BaseEntity
{
    public ContentBulkEditKind Kind { get; set; } = ContentBulkEditKind.TextReplace;

    // Null for runs that are not a find/replace (a structured data refresh has no
    // search term); the history view reads Kind to decide what to show.
    public string? SearchText { get; set; }
    public string? ReplaceText { get; set; }
    public int AffectedPageCount { get; set; }
    public bool IsReverted { get; set; }
    public DateTime? RevertedDate { get; set; }

    public ICollection<ContentBulkEditItem> Items { get; set; } = new List<ContentBulkEditItem>();
}
