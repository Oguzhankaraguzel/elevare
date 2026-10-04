using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.DeleteUser;

public sealed record DeleteUserCommand(Guid Id, Guid RequestedByUserId) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
