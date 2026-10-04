using Application.Features.Commands.Users;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.RoleManagement.CreateRole;

internal sealed record CreateRoleCommandHandler(
    RoleManager<AppRole> RoleManager,
    IRolePermissionCache RolePermissionCache)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await RoleManager.RoleExistsAsync(request.Name))
            return Result.Failure<Guid>(AppRoleErrors.NameAlreadyExists);

        AppRole role = new() { Name = request.Name };
        IdentityResult result = await RoleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            return Result.Failure<Guid>(AppRoleErrors.CreateFailed(result.Describe()));
        }

        Result refreshed = await RolePermissionCache.RefreshAsync(cancellationToken);
        if (refreshed.IsFailure)
            return Result.Failure<Guid>(refreshed.Error);

        return Result.Success(role.Id);
    }
}
