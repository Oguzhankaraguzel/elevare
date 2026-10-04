using SharedKernel.Concrete;

namespace Domain.Entities.Permissions;

public static class RolePermissionErrors
{
    /// <summary>
    /// The live role→permission snapshot could not be rebuilt. Reported to the admin
    /// who triggered it, because the consequence is specific and surprising: their
    /// role edit IS saved, but it will not apply to anyone already signed in until the
    /// snapshot is rebuilt (the next successful role save, or an app restart).
    /// </summary>
    public static Error RefreshFailed(string detail) =>
        Error.Problem(
            "RolePermission.RefreshFailed",
            $"The role was saved, but the live permission cache could not be refreshed, so signed-in users keep their previous permissions until it is: {detail}");
}
