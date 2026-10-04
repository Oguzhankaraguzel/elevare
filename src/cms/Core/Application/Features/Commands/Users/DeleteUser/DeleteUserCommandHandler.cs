using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.DeleteUser;

internal sealed record DeleteUserCommandHandler(UserManager<AppUser> UserManager) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == request.RequestedByUserId)
            return Result.Failure(AppUserErrors.CannotDeleteSelf);

        AppUser? user = await UserManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result.Failure(AppUserErrors.NotFound);

        // Prevent deleting the last SuperAdmin
        IList<AppUser> superAdmins = await UserManager.GetUsersInRoleAsync(Roles.SuperAdmin);
        bool isLastSuperAdmin = superAdmins.Count == 1 && superAdmins[0].Id == request.Id;
        if (isLastSuperAdmin)
            return Result.Failure(AppUserErrors.CannotDeleteLastSuperAdmin);

        await UserManager.DeleteAsync(user);
        return Result.Success();
    }
}
