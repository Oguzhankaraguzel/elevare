using SharedKernel.Concrete;

namespace Domain.Entities.Users;

public static class AppUserErrors
{
    public static readonly Error NotFound = Error.NotFound("AppUser.NotFound", "The user was not found.");
    public static readonly Error EmailAlreadyInUse = Error.Conflict("AppUser.EmailAlreadyInUse", "A user with this e-mail address already exists.");
    public static readonly Error InvalidPassword = Error.Failure("AppUser.InvalidPassword", "The provided password is invalid.");
    public static readonly Error CannotDeleteSelf = Error.Failure("AppUser.CannotDeleteSelf", "You cannot delete your own account.");
    public static readonly Error CannotDeleteLastSuperAdmin = Error.Failure("AppUser.CannotDeleteLastSuperAdmin", "You cannot delete the last SuperAdmin account.");
    public static Error CreateFailed(string details) => Error.Failure("User.CreateFailed", details);
    public static Error UpdateFailed(string details) => Error.Failure("AppUser.UpdateFailed", details);
    public static Error PasswordChangeFailed(string details) => Error.Failure("AppUser.PasswordChangeFailed", details);
}
