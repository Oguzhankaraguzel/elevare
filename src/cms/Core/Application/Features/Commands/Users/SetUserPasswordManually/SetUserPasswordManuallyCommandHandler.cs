using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Users;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.SetUserPasswordManually;

internal sealed class SetUserPasswordManuallyCommandHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IUserContext userContext) : ICommandHandler<SetUserPasswordManuallyCommand>
{
    public async Task<Result> Handle(SetUserPasswordManuallyCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        Result allowed = await PasswordResetGuard.CheckAsync(userManager, userContext, user);
        if (allowed.IsFailure)
            return allowed;

        // Set before the password call, which saves the user — so the flag and the
        // password it belongs to land together or not at all.
        user.MustChangePassword = true;

        IdentityResult result = await userManager.HasPasswordAsync(user)
            ? await ResetExistingPasswordAsync(user, request.NewPassword)
            : await userManager.AddPasswordAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(PasswordSetupErrors.SetFailed(result.Describe()));
        }

        // Any link still out there would let someone choose a password after the
        // administrator already settled the matter.
        await PasswordLinks.RetireAsync(db, user.Id, DateTime.UtcNow, cancellationToken);

        return Result.Success();
    }

    private async Task<IdentityResult> ResetExistingPasswordAsync(AppUser user, string newPassword)
    {
        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        return await userManager.ResetPasswordAsync(user, resetToken, newPassword);
    }
}
