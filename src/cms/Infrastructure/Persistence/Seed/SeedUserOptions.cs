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

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(Email)
        && !string.IsNullOrWhiteSpace(Password);
}
