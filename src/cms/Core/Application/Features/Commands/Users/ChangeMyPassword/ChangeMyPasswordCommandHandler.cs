using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.ChangeMyPassword;

internal sealed class ChangeMyPasswordCommandHandler(
    UserManager<AppUser> userManager,
    IUserContext userContext) : ICommandHandler<ChangeMyPasswordCommand>
{
    public async Task<Result> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(userContext.UserId.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        IdentityResult result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (result.Succeeded)
            return Result.Success();

        // The one failure worth naming ourselves: "you typed the wrong current
        // password" is the common case and deserves a translated message. Policy
        // failures ("must contain a digit") keep Identity's own wording, which is
        // more specific than anything a fixed translation could say.
        return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
            ? Result.Failure(AppUserErrors.InvalidPassword)
            : Result.Failure(AppUserErrors.PasswordChangeFailed(result.Describe()));
    }
}
