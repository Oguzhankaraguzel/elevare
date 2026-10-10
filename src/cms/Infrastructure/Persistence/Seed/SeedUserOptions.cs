namespace Persistence.Seed;

public sealed class SeedUserOptions
{
    public string UserName { get; init; } = "";

    public string Email { get; init; } = "";

    /// <summary>
    /// Empty means "do not create this account". The seeder refuses to invent a
    /// password of its own: a default that ships in the source is the same as no
    /// password at all once the repository is public.
    /// </summary>
    public string Password { get; init; } = "";

    /// <summary>
    /// The way back in for an administrator locked out of a site with no mail server:
    /// on start-up, the existing account's password is set back to <see cref="Password"/>,
    /// and the account unlocked and reactivated. Only read for the SuperAdmin. Meant
    /// to be switched on for one restart and off again — while it is on, every start
    /// logs a warning, and a password changed in the meantime is reset again.
    /// </summary>
    public bool ResetPassword { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password);
}
