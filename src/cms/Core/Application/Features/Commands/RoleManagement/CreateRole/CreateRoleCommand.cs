using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.RoleManagement.CreateRole;

public sealed record CreateRoleCommand(string Name) : ICommand<Guid>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RolesManage;
}
