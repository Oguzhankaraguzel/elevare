using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users;

/// <summary>
/// Who may put a new password on someone else's account. Managing users is enough
/// for everyone but a SuperAdmin: resetting that account's password is taking it over,
/// so only another SuperAdmin may.
/// </summary>
internal static class PasswordResetGuard
{
    public static async Task<Result> CheckAsync(UserManager<AppUser> userManager, IUserContext caller, AppUser target)
    {
        if (!target.IsActive)
            return Result.Failure(PasswordSetupErrors.UserInactive);

        if (!caller.IsInRole(Roles.SuperAdmin) && await userManager.IsInRoleAsync(target, Roles.SuperAdmin))
            return Result.Failure(PasswordSetupErrors.SuperAdminOnly);

        return Result.Success();
    }
}
