namespace Application.Features.Queries.Users.GetUsers;

public sealed record UserResponse(
    Guid Id,
    string? Email,
    string? UserName,
    string? FirstName,
    string? LastName,
    string FullName,
    string? Avatar,   // response calls it Avatar; CA1054 refuses a string named *Url
    string? Bio,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreateDate,
    DateTime? LastLoginDate,
    IReadOnlyList<string> Roles,
    /// <summary>False means the account still can't sign in — see <c>PasswordSetupToken</c>.</summary>
    bool HasPassword,
    /// <summary>
    /// Expiry of the most recent outstanding setup link, when <see cref="HasPassword"/>
    /// is false and a link has been sent at least once. Past this moment (or null with
    /// HasPassword false and CreateDate long ago) the link needs a resend.
    /// </summary>
    DateTime? PendingSetupExpiresAtUtc);
