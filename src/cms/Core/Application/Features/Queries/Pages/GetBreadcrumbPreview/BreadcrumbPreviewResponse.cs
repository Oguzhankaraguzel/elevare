namespace Application.Features.Queries.Pages.GetBreadcrumbPreview;

/// <param name="HomePath">The homepage of the page's language: "/" or "/en".</param>
/// <param name="IsHome">The page is that homepage — no home crumb above it.</param>
/// <param name="Crumbs">The ancestors, outermost first, then the page itself.</param>
public sealed record BreadcrumbPreviewResponse(string HomePath, bool IsHome, IReadOnlyList<BreadcrumbPreviewCrumb> Crumbs);
