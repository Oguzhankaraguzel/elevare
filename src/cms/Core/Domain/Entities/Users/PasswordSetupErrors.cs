using SharedKernel.Concrete;

namespace Domain.Entities.Users;

public static class PasswordSetupErrors
{
    public static readonly Error InvalidOrExpiredToken = Error.Failure(
        "PasswordSetup.InvalidOrExpiredToken",
        "This link is invalid or has expired. Ask an administrator to send a new one.");

    public static readonly Error AlreadyHasPassword = Error.Conflict(
        "PasswordSetup.AlreadyHasPassword",
        "This user already has a password set.");

    public static Error SetFailed(string details) => Error.Failure("PasswordSetup.SetFailed", details);
}
