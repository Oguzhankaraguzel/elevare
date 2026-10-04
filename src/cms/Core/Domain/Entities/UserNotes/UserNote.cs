using Domain.Entities.Abstractions;
using Domain.Entities.Users;

namespace Domain.Entities.UserNotes;

public class UserNote : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public NoteType Type { get; set; } = NoteType.Personal;
    public NoteVisibility Visibility { get; set; } = NoteVisibility.Private;
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    public AppUser User { get; set; } = null!;
}
