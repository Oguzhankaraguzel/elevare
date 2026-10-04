using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Users.UpdateMyProfile;

/// <summary>
/// Edits only the fields that are the account holder's own business. Email, username,
/// roles and the active flag are absent on purpose — those decide who you are and what
/// you may do, and letting a user rewrite them about themselves would route around
/// <see cref="UpdateUser.UpdateUserCommand"/>'s permission gate entirely.
/// </summary>
public sealed record UpdateMyProfileCommand(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? Avatar,
    string? Bio) : ICommand;
