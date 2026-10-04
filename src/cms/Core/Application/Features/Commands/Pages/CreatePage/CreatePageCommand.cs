using Application.Abstraction.Security;
using Domain.Entities.PageInfos;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.CreatePage;

/// <summary>
/// Creates a new (empty) GrapeJS builder page. Returns the new page id so the
/// editor can switch into edit mode and load the visual canvas.
/// </summary>
/// <param name="ParentPageId">Optional parent for category-style nesting (e.g.
/// "hekimler" → "hekimler/sami-sokucu"), up to <see cref="PageHierarchy.MaxDepth"/>
/// levels deep. Must be a page in the same <paramref name="LanguageId"/>.</param>
/// <param name="Kind">CMS-only classification, picked at creation so the page list is
/// readable from the first day. Changeable later from the editor.</param>
public sealed record CreatePageCommand(
    string Title,
    string? Slug,
    int LanguageId,
    int? ParentPageId = null,
    PageKind Kind = PageKind.Unspecified) : ICommand<int>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesCreate;
}
