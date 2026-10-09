using Application.Abstraction.Services.Authentication;

namespace Cms.Tests.Support;

/// <summary>The signed-in user, changeable mid-test; permissions are all granted.</summary>
internal sealed class SettableUserContext : IUserContext
{
    public Guid UserId { get; set; } = Guid.Parse("7d4b0c2e-1f3a-4e5b-9c6d-8a7b6c5d4e3f");
    public bool IsAdminOrAbove => true;
    public bool CanAuthorCustomCode => true;
    public bool HasPermission(string permissionKey) => true;

    /// <summary>Null (the default) belongs to every role; a set narrows it to those.</summary>
    public HashSet<string>? Roles { get; set; }

    public bool IsInRole(string roleName) => Roles is null || Roles.Contains(roleName);
}
