using System.Security.Claims;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Permissions;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.RoleManagement.UpdateRolePermissions;

internal sealed record UpdateRolePermissionsCommandHandler(
    RoleManager<AppRole> RoleManager,
    IRolePermissionCache RolePermissionCache)
    : ICommandHandler<UpdateRolePermissionsCommand>
{
    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        AppRole? role = await RoleManager.FindByIdAsync(request.RoleId.ToString());
        if (role is null)
            return Result.Failure(AppRoleErrors.NotFound);

        IList<Claim> currentClaims = await RoleManager.GetClaimsAsync(role);
        var current = currentClaims
            .Where(c => c.Type == PermissionKeys.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();
        var desired = request.Permissions.ToHashSet();

        foreach (string toAdd in desired.Except(current))
            await RoleManager.AddClaimAsync(role, new Claim(PermissionKeys.ClaimType, toAdd));

        foreach (Claim toRemove in currentClaims.Where(c => c.Type == PermissionKeys.ClaimType && !desired.Contains(c.Value)))
            await RoleManager.RemoveClaimAsync(role, toRemove);

        // Apply to everyone who is already signed in, not just to their next
        // sign-in — revocation in particular must not wait (see IRolePermissionCache).
        // The claim writes above are already committed, so a failed refresh is
        // reported rather than retried: the admin needs to know their change is
        // stored but not yet live.
        return await RolePermissionCache.RefreshAsync(cancellationToken);
    }
}
