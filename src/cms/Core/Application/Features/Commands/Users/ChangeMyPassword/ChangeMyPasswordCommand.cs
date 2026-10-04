using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.ChangeMyPassword;

/// <summary>
/// Requires the current password even though the caller is already signed in: an
/// unattended session should not be enough to lock the real owner out.
/// </summary>
public sealed record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
