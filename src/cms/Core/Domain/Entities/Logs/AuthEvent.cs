using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Logs;

/// <summary>
/// One authentication event — a successful/failed login, a logout, or a stale
/// (expired) token presented back to the server. Read by the SuperAdmin-only
/// "Oturum Kayıtları" screen so it's possible to see who signed in/out and when a
/// session went stale, filter it and export it to a spreadsheet.
/// <para>
/// Deliberately NOT a <see cref="Abstractions.BaseEntity"/>, same reasoning as
/// <see cref="AppLog"/>: a failed login for an unknown identifier has no real user
/// to hang a CreateUserId FK off, and this is append-only audit trail, not editable
/// content — nothing here is ever soft-deleted or updated after the fact.
/// </para>
/// </summary>
public class AuthEvent
{
    public long Id { get; set; }

    public AuthEventType EventType { get; set; }

    /// <summary>
    /// The account this event is about, when one could be resolved — null for a
    /// failed login against an identifier that doesn't match any user (there is
    /// nothing to point the FK at, and that's exactly the case an operator most
    /// wants visible: someone probing usernames that don't exist).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// The identifier as typed/known at the time (email or user name for a login
    /// attempt, the account's user name for logout/session-expiry) — captured here
    /// rather than only via <see cref="UserId"/> so the row still reads sensibly
    /// after a user is renamed or deleted, and so a failed attempt against a
    /// non-existent account still shows what was typed.
    /// </summary>
    [MaxLength(256)]
    public required string UserNameSnapshot { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
