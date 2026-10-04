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
/// The raw token that goes in the e-mailed URL is never stored — only its SHA-256
/// hash, in <see cref="TokenHash"/> — so a database leak alone can't be used to set
/// anyone's password. A fresh row is created every time a link is (re)sent; an
/// older, still-unexpired row for the same user simply keeps working alongside the
/// newest one until it is consumed or expires on its own. That's a deliberate
/// simplification: the old link isn't a live secret at that point (it's already in
/// the recipient's own inbox), so leaving it valid costs nothing.
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

    /// <summary>Null while the link is still outstanding.</summary>
    public DateTime? ConsumedAtUtc { get; set; }
}
