using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.SetUserPasswordManually;

/// <summary>
/// The "yerine şifre belirle" escape hatch — for when a link is no use either (the
/// user is standing right there, or has no way to open one). The password is one the
/// administrator knows, so it only gets the user as far as choosing their own: the
/// account is flagged and the next sign-in leads straight to that step.
/// </summary>
public sealed record SetUserPasswordManuallyCommand(Guid UserId, string NewPassword) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
