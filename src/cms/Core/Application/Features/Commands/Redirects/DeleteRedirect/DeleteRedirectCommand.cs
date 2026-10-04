using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Redirects.DeleteRedirect;

public sealed record DeleteRedirectCommand(int Id) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.RedirectsManage;
}
