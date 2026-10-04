using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.SetPassword;

/// <summary>
/// Deliberately does NOT implement <c>IRequirePermission</c> — this is the one
/// command in the Users feature that an anonymous visitor is meant to reach. The
/// token itself, not a signed-in identity, is what authorizes it (see
/// <see cref="Domain.Entities.Users.PasswordSetupToken"/>).
/// </summary>
public sealed record SetPasswordCommand(string Token, string NewPassword) : ICommand;
