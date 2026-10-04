using System.Security.Claims;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Permissions;
using Hangfire.Dashboard;

namespace Wasm;

/// <summary>
/// Restricts Hangfire dashboard access to authenticated users holding the
/// <see cref="PermissionKeys.HangfireAccess"/> permission — grantable to any role
/// from /admin/roles, not hardcoded to SuperAdmin.
/// <para>
/// Resolves <see cref="IRolePermissionCache"/> from the request services rather
/// than reading the principal's own "permission" claims: those are a snapshot
/// taken at sign-in, so a revoked permission would still open the dashboard until
/// the user signed out. This mirrors <c>UserContext.HasPermission</c>.
/// </para>
/// </summary>
public sealed class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        HttpContext? httpContext = context.GetHttpContext();
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
            return false;

        if (httpContext.RequestServices.GetService(typeof(IRolePermissionCache)) is not IRolePermissionCache rolePermissions)
            return false;

        string[] roleNames = [.. httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value)];
        return roleNames.Length > 0
            && rolePermissions.IsGranted(roleNames, PermissionKeys.HangfireAccess);
    }
}
