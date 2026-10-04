using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Users.GetAssignableUsers;

internal sealed record GetAssignableUsersQueryHandler(UserManager<AppUser> UserManager)
    : IQueryHandler<GetAssignableUsersQuery, List<AssignableUserResponse>>
{
    public async Task<Result<List<AssignableUserResponse>>> Handle(
        GetAssignableUsersQuery request, CancellationToken cancellationToken)
    {
        // FullName is a computed property on the entity, so the projection happens
        // client-side; only the three columns it needs are pulled from the database.
        var rows = await UserManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);

        List<AssignableUserResponse> users = [.. rows.Select(u => new AssignableUserResponse(
            u.Id, u.UserName, $"{u.FirstName} {u.LastName}".Trim()))];

        return Result.Success(users);
    }
}
