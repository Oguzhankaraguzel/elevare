using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Users.GetUsers;

/// <summary>
/// Flexible user list query.
/// All filter parameters are optional — omitting them returns all users.
/// <para>
/// Requires <c>Users.Manage</c>: the response carries e-mail addresses, role
/// membership and sign-in history, which is a staff directory rather than page
/// content. Pickers that only need a name use <c>GetAssignableUsersQuery</c>,
/// which is open to every signed-in user.
/// </para>
/// </summary>
public sealed record GetUsersQuery(
    Guid? Id = null,
    string? Email = null,
    string? UserName = null,
    string? Role = null,
    bool? IsActive = null,
    bool? EmailConfirmed = null,
    DateTime? CreatedAfter = null,
    DateTime? CreatedBefore = null,
    DateTime? LastLoginAfter = null,
    string OrderBy = "CreateDate",
    bool OrderDesc = true,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<UserResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.UsersManage;
}

