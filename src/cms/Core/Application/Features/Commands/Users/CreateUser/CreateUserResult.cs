namespace Application.Features.Commands.Users.CreateUser;

/// <summary>
/// <paramref name="EmailSent"/> is false when the user was created successfully but
/// the password-setup e-mail failed to send (bad SMTP config, provider outage,
/// etc.) — not fatal to user creation (see the handler's own comment), but the UI
/// needs to know so it can tell the admin to use "resend" from the Users list
/// instead of assuming the new user already has a link in their inbox.
/// </summary>
public sealed record CreateUserResult(Guid UserId, bool EmailSent);
