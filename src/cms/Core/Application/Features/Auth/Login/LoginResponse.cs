namespace Application.Features.Auth.Login;

#pragma warning disable CA1054
public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    string Token,
    DateTime ExpiresAt);
#pragma warning restore CA1054
