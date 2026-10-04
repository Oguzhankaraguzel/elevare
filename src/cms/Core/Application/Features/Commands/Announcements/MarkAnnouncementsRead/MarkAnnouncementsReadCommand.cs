using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Announcements.MarkAnnouncementsRead;

/// <summary>
/// Marks every announcement the caller can see as read by the caller. Opening the
/// bell is the acknowledgement, so there is nothing to pass in.
/// </summary>
public sealed record MarkAnnouncementsReadCommand : ICommand;
