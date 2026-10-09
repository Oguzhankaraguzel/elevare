namespace Application.Features.Auth.Login;

/// <param name="PasswordChangeToken">
/// Set instead of <paramref name="Token"/> when the password was one an administrator
/// typed: the sign-in is not finished until the user chooses their own, with this
/// one-time token. <paramref name="Token"/> is empty then — no session is handed out.
/// </param>
#pragma warning disable CA1054
public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    string Token,
    DateTime ExpiresAt,
    string? PasswordChangeToken = null);
#pragma warning restore CA1054
