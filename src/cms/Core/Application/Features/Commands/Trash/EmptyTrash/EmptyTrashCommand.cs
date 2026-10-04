using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Trash.EmptyTrash;

/// <summary>
/// Purges everything currently in the Trash, and reports how much it could not.
/// Items blocked by a safety check (a page holding form submissions, a page with
/// sub-pages) are skipped rather than failing the whole run — one protected page
/// must not stop the other fifty from being cleared.
/// </summary>
public sealed record EmptyTrashCommand : ICommand<EmptyTrashResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TrashRestore;
}
