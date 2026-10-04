using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.CreateUser;

// CA1054 asks for a Uri-typed parameter, but CmsBaseUrl comes straight from
// NavigationManager.BaseUri (already a string) and is only ever string-concatenated
// into an e-mail link, never parsed as a Uri.
#pragma warning disable CA1054
public sealed record CreateUserCommand(
    string Email,
    string UserName,
    string? FirstName,
    string? LastName,
    string? Bio,
    string Role,
    /// <summary>
    /// The CMS's own origin (e.g. <c>https://cms.example.com</c>), used to build the
    /// password-setup link in the e-mail. Threaded in from the caller rather than
    /// resolved from HttpContext inside the handler: this command runs from a Blazor
    /// Server circuit, where HttpContext is not reliably available (see
    /// InfrastructureServiceRegistration's note on IHttpContextAccessor), but
    /// NavigationManager.BaseUri always is.
    /// </summary>
    string CmsBaseUrl,
    bool IsActive = true) : ICommand<CreateUserResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}
#pragma warning restore CA1054
