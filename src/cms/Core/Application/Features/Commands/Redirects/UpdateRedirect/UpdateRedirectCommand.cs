using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Redirects.UpdateRedirect;

// See CreateRedirectCommand for why NewPath is a string.
#pragma warning disable CA1054
public sealed record UpdateRedirectCommand(int Id, string OldPath, string? NewPath)
#pragma warning restore CA1054
    : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RedirectsManage;
}
