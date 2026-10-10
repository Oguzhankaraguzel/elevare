using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Users;

/// <summary>
/// A one-time, time-limited link that lets a newly created user pick their own
/// password, instead of an admin typing one on their behalf. Deliberately its own
/// table rather than ASP.NET Identity's built-in
/// <c>UserManager.GeneratePasswordResetTokenAsync</c>: that token is opaque and
/// never persisted, so there would be no row to show a SuperAdmin "this link
/// expires in 3 days" or "this link already expired" — exactly the visibility the
/// resend/set-manually screen needs.
/// <para>
/// The raw token that goes in the URL is never stored — only its SHA-256 hash, in
/// <see cref="TokenHash"/> — so a database leak alone can't be used to set anyone's
/// password. A fresh row is created every time a link is issued, and issuing one
/// retires every link the user still had: a link may have been copied into a chat
/// rather than mailed, and only the newest one should still open the door.
/// </para>
/// <para>
/// The same row serves three purposes — the first link of a new account, a reset an
/// administrator or the user asked for, and the short step between signing in with
/// a password an administrator typed and choosing one's own. Only the lifetime
/// differs; see PasswordLinks.
/// </para>
/// </summary>
public class PasswordSetupToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(128)]
    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Null while the link is still outstanding; also set when a newer link retires it.</summary>
    public DateTime? ConsumedAtUtc { get; set; }
}
