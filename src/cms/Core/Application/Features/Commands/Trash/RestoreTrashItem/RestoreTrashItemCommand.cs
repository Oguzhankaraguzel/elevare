using Application.Features.Trash;
using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Trash.RestoreTrashItem;

/// <param name="OverrideSlug">
/// Only meaningful for <see cref="TrashEntityType.PageInfo"/>: used when the page's
/// original slug is now taken by another live page, letting the admin restore it
/// under a different slug instead.
/// </param>
public sealed record RestoreTrashItemCommand(TrashEntityType EntityType, int Id, string? OverrideSlug = null) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.TrashRestore;
}
