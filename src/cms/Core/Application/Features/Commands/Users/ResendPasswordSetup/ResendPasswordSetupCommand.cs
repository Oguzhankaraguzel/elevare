using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.ResendPasswordSetup;

/// <summary>
/// The "yeniden gönder" action for a user whose original setup link expired (or was
/// lost) before they picked a password — e.g. the 7-day link went out but the
/// person only came back on day 8.
/// </summary>
// CA1054 asks for a Uri-typed parameter, but CmsBaseUrl comes straight from
// NavigationManager.BaseUri (already a string) and is only ever string-concatenated
// into an e-mail link, never parsed as a Uri.
#pragma warning disable CA1054
public sealed record ResendPasswordSetupCommand(Guid UserId, string CmsBaseUrl) : ICommand<bool>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
#pragma warning restore CA1054
