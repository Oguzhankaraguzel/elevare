using System.Security.Claims;
using AppRoles = Domain.Entities.Users.Roles;
using Domain.Entities.Permissions;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.RoleManagement.GetRoles;

internal sealed record GetRolesQueryHandler(RoleManager<AppRole> RoleManager)
    : IQueryHandler<GetRolesQuery, List<RoleResponse>>
{
    private static readonly HashSet<string> BuiltInRoleNames =
    [
        AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Developer, AppRoles.Editor, AppRoles.Author, AppRoles.Viewer,
    ];

    public async Task<Result<List<RoleResponse>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        List<AppRole> roles = await RoleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        List<RoleResponse> responses = [];
        foreach (AppRole role in roles)
        {
            IList<Claim> claims = await RoleManager.GetClaimsAsync(role);
            var permissions = claims
                .Where(c => c.Type == PermissionKeys.ClaimType)
                .Select(c => c.Value)
                .ToList();

            responses.Add(new RoleResponse(
                role.Id, role.Name ?? "", BuiltInRoleNames.Contains(role.Name ?? ""), permissions));
        }

        return Result.Success(responses);
    }
}
