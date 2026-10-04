using Domain.Entities.PageInfos;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.UpdatePageStatus;

/// <summary>
/// Changes only a page's <see cref="PageStatus"/> (used by the Pages list's inline
/// "Publish" edit) — unlike <see cref="UpdatePage.UpdatePageCommand"/>, this never
/// touches the page's title, SEO fields, or GrapeJS content.
/// </summary>
/// <param name="RedirectTargetUrl">
/// Optional destination for visitors hitting the page's old URL after it leaves
/// Published — null means no redirect. Only meaningful when moving away from Published.
/// </param>
#pragma warning disable CA1054
public sealed record UpdatePageStatusCommand(int Id, PageStatus Status, string? RedirectTargetUrl = null) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesPublish;
}
#pragma warning restore CA1054
