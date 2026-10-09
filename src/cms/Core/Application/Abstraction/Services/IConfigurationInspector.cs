namespace Application.Abstraction.Services;

/// <summary>
/// Reads the server's own configuration — appsettings.json and environment variables
/// (a Docker <c>.env</c>) — for the screens that otherwise only see the database.
/// Without it, a CMS whose mail settings come from <c>.env</c> shows every mail
/// field empty, as if mail were not set up at all, while it works.
/// </summary>
public interface IConfigurationInspector
{
    /// <summary>
    /// The value the server's configuration gives <paramref name="key"/>, ignoring
    /// anything saved on the Secrets screen — that is, what the CMS falls back to
    /// when the screen's value is cleared. Null when it gives none, or an empty one.
    /// </summary>
    string? GetServerValue(string key);

    /// <summary>The value in effect right now, from wherever it comes. Null when empty.</summary>
    string? GetEffectiveValue(string key);
}
