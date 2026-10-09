using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.Users;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    /// <summary>Relative or absolute URL to the user's profile picture.</summary>
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set when an administrator typed this user's password for them. The user has to
    /// pick their own before they get a session — until then the password is one
    /// somebody else knows. See LoginCommandHandler and SetPasswordCommandHandler.
    /// </summary>
    public bool MustChangePassword { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
