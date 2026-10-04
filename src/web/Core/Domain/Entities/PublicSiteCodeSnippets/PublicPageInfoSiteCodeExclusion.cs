namespace Domain.Entities.PublicSiteCodeSnippets;

/// <summary>Read-only projection of the CMS's <c>PageInfoSiteCodeExclusions</c> join
/// table: "this site code does not run on this page". Queried directly, like
/// <c>PublicPageInfoTag</c> — the layout only ever needs the ids for one page.</summary>
public sealed class PublicPageInfoSiteCodeExclusion
{
    public int PageInfoId { get; set; }
    public int SiteCodeSnippetId { get; set; }
}
