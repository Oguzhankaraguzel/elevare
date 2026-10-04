using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Users.GetMyProfile;

internal sealed class GetMyProfileQueryHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IUserContext userContext) : IQueryHandler<GetMyProfileQuery, MyProfileResponse>
{
    public async Task<Result<MyProfileResponse>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(userContext.UserId.ToString());
        if (user is null)
            return Result.Failure<MyProfileResponse>(AppUserErrors.NotFound);

        IList<string> roles = await userManager.GetRolesAsync(user);

        int pagesAuthored = await db.PageInfos
            .CountAsync(p => p.CreateUserId == user.Id, cancellationToken);

        return Result.Success(new MyProfileResponse(
            user.Id,
            user.Email ?? "",
            user.UserName ?? "",
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.AvatarUrl,   // response calls it Avatar; CA1054 refuses a string named *Url
            user.Bio,
            user.CreateDate,
            user.LastLoginDate,
            [.. roles],
            pagesAuthored));
    }
}
