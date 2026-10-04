using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    UserManager<AppUser> userManager,
    IUserContext userContext) : ICommandHandler<UpdateMyProfileCommand>
{
    public async Task<Result> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(userContext.UserId.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        user.FirstName = Trim(request.FirstName);
        user.LastName = Trim(request.LastName);
        user.AvatarUrl = Trim(request.Avatar);
        user.Bio = Trim(request.Bio);

        // Identity validates and normalizes the phone number itself, and it is the
        // only one of these fields it tracks.
        IdentityResult phone = await userManager.SetPhoneNumberAsync(user, Trim(request.PhoneNumber));
        if (!phone.Succeeded)
            return Result.Failure(AppUserErrors.UpdateFailed(phone.Describe()));

        IdentityResult update = await userManager.UpdateAsync(user);
        return update.Succeeded ? Result.Success() : Result.Failure(AppUserErrors.UpdateFailed(update.Describe()));
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
