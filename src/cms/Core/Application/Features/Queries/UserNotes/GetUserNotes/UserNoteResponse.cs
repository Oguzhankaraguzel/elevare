using Domain.Entities.UserNotes;

namespace Application.Features.Queries.UserNotes.GetUserNotes;

public sealed record UserNoteResponse(
    int Id,
    string Title,
    string Content,
    NoteType Type,
    NoteVisibility Visibility,
    string? Color,
    bool IsPinned,
    bool IsArchived,
    Guid UserId,
    bool IsActive,
    DateTime CreateDate,
    string CreatedBy,
    DateTime? UpdateDate,
    string? UpdatedBy);
