using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Bio,
    string? Role,
    bool IsActive) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
