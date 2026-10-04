using SharedKernel.Concrete;

namespace Application.Features.Auth.Login;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Failure("Auth.InvalidCredentials", "Invalid email address or password.");

    public static readonly Error AccountLocked =
        Error.Failure("Auth.AccountLocked", "Your account is locked. Please contact the administrator.");
}
