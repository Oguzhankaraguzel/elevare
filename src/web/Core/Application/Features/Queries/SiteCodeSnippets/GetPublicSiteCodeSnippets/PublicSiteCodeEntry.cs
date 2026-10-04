namespace Application.Features.Queries.SiteCodeSnippets.GetPublicSiteCodeSnippets;

/// <summary>
/// One snippet as the layout receives it. The id travels with the content so a
/// page can leave a snippet out (<c>PageInfoSiteCodeExclusions</c>) without the
/// query — and its 30-second cache — having to know which page is rendering.
/// </summary>
public sealed record PublicSiteCodeEntry(int Id, string Content);
