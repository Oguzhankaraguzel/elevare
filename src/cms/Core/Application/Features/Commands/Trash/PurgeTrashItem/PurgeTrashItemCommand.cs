using Application.Abstraction.Security;
using Application.Features.Trash;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Trash.PurgeTrashItem;

/// <summary>
/// Deletes one trashed item for good. Gated on <see cref="PermissionKeys.TrashRestore"/>
/// rather than a permission of its own: whoever is trusted to decide what comes back
/// is the same person trusted to decide what never does.
/// </summary>
public sealed record PurgeTrashItemCommand(TrashEntityType EntityType, int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TrashRestore;
}
