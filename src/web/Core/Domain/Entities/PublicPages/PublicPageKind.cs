namespace Domain.Entities.PublicPages;

/// <summary>
/// Mirrors the CMS's <c>Domain.Entities.PageInfos.PageKind</c> numeric values — the
/// same independent-copy arrangement as <see cref="PublicPageStatus"/>. The public
/// site only reads it to decide what goes into the article feed.
/// </summary>
public enum PublicPageKind
{
    Unspecified = 0,
    LandingPage = 1,
    Category = 2,
    Article = 3,
    Corporate = 4,
    Legal = 5,
    System = 6,
}
