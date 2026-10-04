using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.UpdateUser;

internal sealed record UpdateUserCommandHandler(UserManager<AppUser> UserManager) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await UserManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Bio = request.Bio;
        user.IsActive = request.IsActive;

        await UserManager.UpdateAsync(user);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            IList<string> currentRoles = await UserManager.GetRolesAsync(user);
            await UserManager.RemoveFromRolesAsync(user, currentRoles);
            await UserManager.AddToRoleAsync(user, request.Role);
        }

        return Result.Success();
    }
}
