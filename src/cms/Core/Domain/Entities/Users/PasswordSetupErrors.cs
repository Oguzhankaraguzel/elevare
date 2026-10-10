using SharedKernel.Concrete;

namespace Domain.Entities.Users;

public static class PasswordSetupErrors
{
    public static readonly Error InvalidOrExpiredToken = Error.Failure(
        "PasswordSetup.InvalidOrExpiredToken",
        "This link is invalid or has expired. Ask an administrator to send a new one.");

    public static readonly Error UserInactive = Error.Failure(
        "PasswordSetup.UserInactive",
        "This account is inactive. Activate it first; a link for an inactive account would not work.");

    /// <summary>
    /// Only a SuperAdmin can reset a SuperAdmin's password. Otherwise anyone allowed
    /// to manage users could take over the most powerful account in the system.
    /// </summary>
    public static readonly Error SuperAdminOnly = Error.Failure(
        "PasswordSetup.SuperAdminOnly",
        "Only a SuperAdmin can reset the password of a SuperAdmin account.");

    public static readonly Error SameAsTemporary = Error.Failure(
        "PasswordSetup.SameAsTemporary",
        "Choose a password different from the one you were given.");

    public static Error SetFailed(string details) => Error.Failure("PasswordSetup.SetFailed", details);
}
