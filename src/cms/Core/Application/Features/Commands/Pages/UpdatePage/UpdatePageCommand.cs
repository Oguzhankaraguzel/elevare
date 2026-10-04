using Domain.Entities.PageInfos;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.UpdatePage;

/// <summary>
/// Saves a builder page: general fields, status, SEO metadata and the GrapeJS
/// output (exported HTML + CSS and the project JSON for restoring the editor).
/// </summary>
/// <param name="ParentPageId">Optional parent for category-style nesting, up to
/// <see cref="Application.Features.Commands.Pages.PageHierarchy.MaxDepth"/> levels
/// deep. Null clears the parent (page becomes top-level).</param>
/// <param name="ExpectedFingerprint">The page as the editor loaded it (see <see cref="PageFingerprint"/>);
/// the save is refused when the page has changed since. Null skips the check.</param>
/// <param name="Overwrite">Save over a change made since, after the author was told about it.</param>
public sealed record UpdatePageCommand(
    int Id,
    string Title,
    string Slug,
    PageStatus Status,
    string? GjsHtml,
    string? GjsCss,
    string? GjsData,
    UpdatePageSeoMeta Seo,
    int? ParentPageId = null,
    List<int>? TagIds = null,
    PageKind Kind = PageKind.Unspecified,
    List<int>? ExcludedSiteCodeSnippetIds = null,
    string? ExpectedFingerprint = null,
    bool Overwrite = false) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesEdit;
}
