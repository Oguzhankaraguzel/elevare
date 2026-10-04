using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Users.GetAssignableUsers;

/// <summary>
/// Active users, name only, for "assign this to someone" pickers.
/// <para>
/// Open to any signed-in user on purpose. Handing a task to a colleague or naming
/// an approver is ordinary work that must not require <c>Users.Manage</c>; the
/// full <c>GetUsersQuery</c> does require it, because it also returns e-mail
/// addresses, roles and sign-in history.
/// </para>
/// </summary>
public sealed record GetAssignableUsersQuery : IQuery<List<AssignableUserResponse>>;
