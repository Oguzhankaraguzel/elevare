using Domain.Entities.Users;

namespace Domain.Entities.UserNotes;

/// <summary>
/// Marks that one person has seen one announcement.
/// <para>
/// An announcement is not its own entity — it is simply a <see cref="UserNote"/> with
/// <see cref="NoteVisibility.Everyone"/>, so the whole authoring, editing and
/// permission story comes for free. The only thing notes cannot answer is "have
/// <em>you</em> read this yet", and that is exactly what this row records.
/// </para>
/// <para>
/// Deliberately not a <see cref="Abstractions.BaseEntity"/>: a read marker has no
/// author, no edits and nothing to soft-delete — the same reasoning as
/// <see cref="Analytics.PageViewHit"/>.
/// </para>
/// </summary>
public class AnnouncementRead
{
    public int Id { get; set; }

    public int UserNoteId { get; set; }
    public UserNote? UserNote { get; set; }

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public DateTime ReadAtUtc { get; set; }
}
