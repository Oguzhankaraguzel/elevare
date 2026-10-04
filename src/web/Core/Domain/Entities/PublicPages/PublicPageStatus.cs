namespace Domain.Entities.PublicPages;

/// <summary>
/// Mirrors the CMS's <c>Domain.Entities.PageInfos.PageStatus</c> numeric values.
/// Kept as an independent copy (rather than referencing the CMS assembly) so the
/// public Web app has no compile-time dependency on the CMS module — it only
/// needs to agree on the stored integer values.
/// </summary>
public enum PublicPageStatus
{
    Draft = 1,
    Published,
    Archived
}
