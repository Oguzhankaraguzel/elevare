namespace Application.Features.Queries.Users.GetMyProfile;

/// <param name="Roles">Read-only here — changing your own role is a Users &amp; Roles job.</param>
/// <param name="PagesAuthored">
/// How many pages this account created. It answers the question people actually
/// have on their own profile — "what am I on the hook for" — and it is one extra
/// count, not a new subsystem.
/// </param>
public sealed record MyProfileResponse(
    Guid Id,
    string Email,
    string UserName,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Avatar,
    string? Bio,
    DateTime CreateDate,
    DateTime? LastLoginDate,
    IReadOnlyList<string> Roles,
    int PagesAuthored);
