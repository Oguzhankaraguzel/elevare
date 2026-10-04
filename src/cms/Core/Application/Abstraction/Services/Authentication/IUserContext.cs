namespace Application.Abstraction.Services.Authentication;

public interface IUserContext
{
    Guid UserId { get; }
    bool IsAdminOrAbove { get; }

    /// <summary>Whether the current user may author raw script/custom-code blocks in the page builder.</summary>
    bool CanAuthorCustomCode { get; }

    /// <summary>
    /// Whether the current user's roles grant the given permission key (see
    /// <see cref="Domain.Entities.Permissions.PermissionKeys"/>). Unlike
    /// <see cref="IsAdminOrAbove"/>/<see cref="CanAuthorCustomCode"/> (fixed role checks),
    /// this reflects whatever a SuperAdmin has configured per role at runtime.
    /// </summary>
    bool HasPermission(string permissionKey);

    /// <summary>Whether the current user is a member of the given role name (e.g. an approval workflow step's required role).</summary>
    bool IsInRole(string roleName);
}
