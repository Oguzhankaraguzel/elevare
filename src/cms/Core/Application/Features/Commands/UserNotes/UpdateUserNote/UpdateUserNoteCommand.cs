using SharedKernel.Abstraction.Messaging;
using Domain.Entities.UserNotes;

namespace Application.Features.Commands.UserNotes.UpdateUserNote;

public sealed record UpdateUserNoteCommand(
    int Id,
    string Title,
    string Content,
    NoteType Type,
    NoteVisibility Visibility,
    string? Color,
    bool IsPinned,
    bool IsArchived,
    bool IsActive) : ICommand;
