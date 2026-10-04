using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;

namespace Application.Features.Commands.UserNotes.CreateUserNote;

public sealed record CreateUserNoteCommand(
    string Title,
    string Content,
    NoteType Type,
    NoteVisibility Visibility,
    string? Color,
    bool IsPinned) : ICommand;
