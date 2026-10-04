using Application.Features.Commands.Users;
using AppRoles = Domain.Entities.Users.Roles;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.RoleManagement.DeleteRole;

internal sealed record DeleteRoleCommandHandler(
    RoleManager<AppRole> RoleManager,
    UserManager<AppUser> UserManager,
    IRolePermissionCache RolePermissionCache,
    ICmsApplicationDbContext Db)
    : ICommandHandler<DeleteRoleCommand>
{
    private static readonly HashSet<string> BuiltInRoleNames =
    [
        AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Developer, AppRoles.Editor, AppRoles.Author, AppRoles.Viewer,
    ];

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        AppRole? role = await RoleManager.FindByIdAsync(request.Id.ToString());
        if (role is null)
            return Result.Failure(AppRoleErrors.NotFound);

        if (BuiltInRoleNames.Contains(role.Name ?? ""))
            return Result.Failure(AppRoleErrors.CannotDeleteBuiltInRole);

        IList<AppUser> usersInRole = await UserManager.GetUsersInRoleAsync(role.Name ?? "");
        if (usersInRole.Count > 0)
            return Result.Failure(AppRoleErrors.CannotDeleteRoleInUse);

        // The FK from WorkflowStep.RequiredRoleId is Restrict, so the database would
        // refuse this delete anyway — but only after RoleManager.DeleteAsync below has
        // already thrown past this handler's own error handling (Identity's own
        // SaveChanges runs inside that call, ahead of the pipeline's). Checking here
        // first turns that into the same clean, expected failure as the "users
        // assigned" case above, active or inactive workflow alike — a step's claim on
        // a role does not depend on whether its definition is currently gating anything.
        bool usedInWorkflow = await Db.WorkflowSteps.AnyAsync(s => s.RequiredRoleId == request.Id, cancellationToken);
        if (usedInWorkflow)
            return Result.Failure(AppRoleErrors.CannotDeleteRoleInWorkflow);

        IdentityResult result = await RoleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return Result.Failure(AppRoleErrors.DeleteFailed(result.Describe()));
        }

        return await RolePermissionCache.RefreshAsync(cancellationToken);
    }
}
