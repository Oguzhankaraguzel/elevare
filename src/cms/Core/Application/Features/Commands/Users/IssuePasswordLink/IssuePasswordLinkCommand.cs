using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.IssuePasswordLink;

/// <summary>
/// A new one-time link for a user to choose their password with — a first password
/// after the original link expired, or a new one after they forgot theirs. Retires
/// every link the user still had.
/// </summary>
/// <param name="SendByEmail">
/// Mail it to the user. False — or a send that fails — returns the link instead, for
/// the administrator to pass on themselves. That is the way in on a site with no mail
/// server.
/// </param>
// CA1054: CmsBaseUrl comes straight from NavigationManager.BaseUri (already a string)
// and is only ever concatenated into a link, never parsed as a Uri.
#pragma warning disable CA1054
public sealed record IssuePasswordLinkCommand(Guid UserId, string CmsBaseUrl, bool SendByEmail)
    : ICommand<PasswordLinkResult>, IRequirePermission
#pragma warning restore CA1054
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
