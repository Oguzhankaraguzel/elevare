using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.RoleManagement.DeleteRole;

public sealed record DeleteRoleCommand(Guid Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RolesManage;
}
