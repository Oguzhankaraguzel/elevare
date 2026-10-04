using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.SetUserPasswordManually;

/// <summary>
/// The "yerine şifre belirle" escape hatch — for when resending the e-mail isn't an
/// option (mail server down, the address is wrong, the user is standing right
/// there). Bypasses the setup-link flow entirely; the SuperAdmin is trusted the same
/// way they already are for every other field on this form.
/// </summary>
public sealed record SetUserPasswordManuallyCommand(Guid UserId, string NewPassword) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
