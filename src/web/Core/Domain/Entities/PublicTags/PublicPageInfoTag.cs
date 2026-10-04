namespace Domain.Entities.PublicTags;

/// <summary>Read-only projection of the CMS's <c>PageInfoTags</c> many-to-many join
/// table. Queried directly (no navigation property) since the Web side only ever
/// needs "which page ids have this tag" / "which tags does this set of pages use".</summary>
public sealed class PublicPageInfoTag
{
    public int PageInfoId { get; set; }
    public int TagId { get; set; }
}
