namespace Domain.Entities.ContentBulkEdits;

/// <summary>
/// What a run did, so the history can describe it honestly and the revert knows
/// which columns to restore.
/// </summary>
public enum ContentBulkEditKind
{
    /// <summary>Find text across page content and replace it.</summary>
    TextReplace = 0,

    /// <summary>
    /// Re-derive the structured data nodes the CMS owns (Organization, WebSite,
    /// breadcrumb) from current site settings and the page tree.
    /// </summary>
    StructuredDataRefresh = 1,
}
