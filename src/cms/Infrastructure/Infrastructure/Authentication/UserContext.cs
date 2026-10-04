using System.Security.Claims;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Permissions;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Http;
using SharedKernel.Extensions.Claims;

namespace Infrastructure.Authentication;

internal sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserPrincipalHolder _principalHolder;
    private readonly IRolePermissionCache _rolePermissions;

    public UserContext(
        IHttpContextAccessor httpContextAccessor,
        CurrentUserPrincipalHolder principalHolder,
        IRolePermissionCache rolePermissions)
    {
        _httpContextAccessor = httpContextAccessor;
        _principalHolder     = principalHolder;
        _rolePermissions     = rolePermissions;
    }

    public Guid UserId => GetUserIdFromToken(_httpContextAccessor, _principalHolder);

    public bool IsAdminOrAbove
    {
        get
        {
            ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User
                ?? _principalHolder.Principal;
            if (principal?.Identity?.IsAuthenticated != true) return false;
            return principal.IsInRole(Roles.SuperAdmin)
                || principal.IsInRole(Roles.Admin);
        }
    }

    /// <summary>
    /// Answers from the <see cref="PermissionKeys.CustomCodeAuthor"/> grant, not from
    /// a fixed list of role names.
    /// <para>
    /// It used to hardcode SuperAdmin/Admin/Developer, which made the matching
    /// checkbox on /admin/roles a lie in both directions: granting the permission to
    /// another role changed nothing, and revoking it from Developer left them still
    /// able to write script. The permission is the thing an operator can see and
    /// reason about, so it has to be the thing that decides.
    /// </para>
    /// </summary>
    public bool CanAuthorCustomCode => HasPermission(PermissionKeys.CustomCodeAuthor);

    public bool HasPermission(string permissionKey)
    {
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User
            ?? _principalHolder.Principal;
        if (principal?.Identity?.IsAuthenticated != true) return false;

        // Resolved from the live role→permission snapshot rather than from the
        // principal's own "permission" claims. Identity copies AspNetRoleClaims into
        // the principal only once, at sign-in, so reading them here would leave an
        // already-signed-in user holding permissions a SuperAdmin has since revoked.
        // The user's ROLE MEMBERSHIP still comes from the principal — changing which
        // roles a user belongs to does require them to sign in again.
        string[] roleNames = [.. principal.FindAll(ClaimTypes.Role).Select(c => c.Value)];
        return roleNames.Length > 0 && _rolePermissions.IsGranted(roleNames, permissionKey);
    }

    public bool IsInRole(string roleName)
    {
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User
            ?? _principalHolder.Principal;
        if (principal?.Identity?.IsAuthenticated != true) return false;
        return principal.IsInRole(roleName);
    }

    private Guid GetUserIdFromToken(IHttpContextAccessor httpContextAccessor, CurrentUserPrincipalHolder principalHolder) 
    {
        // ── Source 1: classic HTTP pipeline (API endpoints, SSR pre-render) ─
        ClaimsPrincipal? httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            try
            { return httpUser.GetUserId(); }
            catch { /* claim missing or malformed — fall through */ }
        }

        // ── Source 2: Blazor Server SignalR circuit ────────────────────────
        if (principalHolder.Principal?.Identity?.IsAuthenticated == true)
        {
            try
            { return _principalHolder.Principal.GetUserId(); }
            catch { /* claim missing or malformed — fall through */ }
        }

        throw new ApplicationException("User context is unavailable");
    }
}

