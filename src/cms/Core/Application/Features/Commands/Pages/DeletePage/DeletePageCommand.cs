using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Pages.DeletePage;

/// <param name="RedirectTargetUrl">
/// Optional destination for visitors hitting the page's old URL after deletion —
/// null means no redirect (today's behavior: they see a 404). Only meaningful
/// when the page being deleted was Published.
/// </param>
#pragma warning disable CA1054
/// <param name="ConfirmHomePageUnpublish">Required to delete a published homepage — see <see cref="HomePageUnpublish"/>.</param>
public sealed record DeletePageCommand(int Id, string? RedirectTargetUrl = null, bool ConfirmHomePageUnpublish = false) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.PagesDelete;
}
#pragma warning restore CA1054
