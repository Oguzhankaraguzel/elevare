namespace Application.Features.Queries.Users.GetAssignableUsers;

/// <summary>
/// The bare minimum needed to put a person in a picker: who they are and what to
/// show. Deliberately carries no e-mail, role, or sign-in history — this list is
/// readable by every signed-in user, so anything extra here becomes a directory
/// leak (see <c>GetAssignableUsersQuery</c>).
/// </summary>
public sealed record AssignableUserResponse(Guid Id, string? UserName, string FullName);
