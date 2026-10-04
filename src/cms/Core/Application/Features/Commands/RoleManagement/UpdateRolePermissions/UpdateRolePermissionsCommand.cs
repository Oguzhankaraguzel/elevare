using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.RoleManagement.UpdateRolePermissions;

/// <summary>Replaces a role's entire permission set with <paramref name="Permissions"/> (diffed against its current claims).</summary>
public sealed record UpdateRolePermissionsCommand(Guid RoleId, List<string> Permissions) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RolesManage;
}
