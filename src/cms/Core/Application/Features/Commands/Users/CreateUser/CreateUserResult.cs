namespace Application.Features.Commands.Users.CreateUser;

/// <summary>
/// <paramref name="EmailSent"/> is false when the user was created but the
/// password-setup e-mail did not go out — no mail server is configured, or the send
/// failed. Not fatal to user creation: the link exists either way, and is returned in
/// <paramref name="SetupUrl"/> so the administrator can pass it on themselves.
/// </summary>
/// <param name="SetupUrl">The setup link, only when it was not mailed.</param>
// CA1054/CA1056: a link built for an administrator to copy, never parsed as a Uri.
#pragma warning disable CA1054, CA1056
public sealed record CreateUserResult(Guid UserId, bool EmailSent, string? SetupUrl, DateTime LinkExpiresAtUtc);
#pragma warning restore CA1054, CA1056
