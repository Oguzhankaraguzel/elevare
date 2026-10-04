using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.UserNotes.DeleteUserNote;

public sealed record DeleteUserNoteCommand(int Id) : ICommand;
