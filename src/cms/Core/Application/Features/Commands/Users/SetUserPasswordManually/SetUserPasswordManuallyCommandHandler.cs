using Application.Features.Commands.Users;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.SetUserPasswordManually;

internal sealed class SetUserPasswordManuallyCommandHandler(UserManager<AppUser> userManager)
    : ICommandHandler<SetUserPasswordManuallyCommand>
{
    public async Task<Result> Handle(SetUserPasswordManuallyCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        IdentityResult result = await userManager.HasPasswordAsync(user)
            ? await ResetExistingPasswordAsync(user, request.NewPassword)
            : await userManager.AddPasswordAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(PasswordSetupErrors.SetFailed(result.Describe()));
        }

        return Result.Success();
    }

    private async Task<IdentityResult> ResetExistingPasswordAsync(AppUser user, string newPassword)
    {
        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        return await userManager.ResetPasswordAsync(user, resetToken, newPassword);
    }
}
