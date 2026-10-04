using System.ComponentModel.DataAnnotations;

namespace Persistence.Seed;

/// <summary>
/// The first accounts the system creates for itself, read from configuration
/// (<c>Seed</c> section) so a deployment is never stuck with credentials that are
/// published in the repository.
/// <para>
/// Anything here can also be supplied as an environment variable —
/// <c>Seed__SuperAdmin__Password</c> — which is how the Docker setup passes it
/// without writing it to a file.
/// </para>
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    [Required]
    public SeedUserOptions SuperAdmin { get; init; } = new();

    /// <summary>
    /// The demo editor account. Off unless a password is supplied — a second
    /// known login is a liability on a real deployment and a convenience only
    /// while trying the project out.
    /// </summary>
    public SeedUserOptions? Editor { get; init; }
}
