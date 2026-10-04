using SharedKernel.Concrete;

namespace Application.Abstraction.Security;

public static class PermissionErrors
{
    /// <summary>
    /// The caller lacks the permission the request declared.
    /// <para>
    /// The key is named in the message on purpose: an operator seeing this needs to
    /// know which checkbox on /admin/roles to look at, and the key is the only thing
    /// that maps one-to-one onto it.
    /// </para>
    /// </summary>
    public static Error NotGranted(string permissionKey) => Error.Failure(
        "Permission.NotGranted",
        $"This action needs the '{permissionKey}' permission, which your role does not have.");
}
