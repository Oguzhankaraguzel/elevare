using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.Users;

/// <summary>
/// Identity role. Permissions are attached to roles as claims (<c>AspNetRoleClaims</c>,
/// see <c>PermissionKeys.ClaimType</c>) rather than as properties here — that is what
/// lets a SuperAdmin define custom roles and their permissions at runtime.
/// </summary>
public class AppRole : IdentityRole<Guid>;
