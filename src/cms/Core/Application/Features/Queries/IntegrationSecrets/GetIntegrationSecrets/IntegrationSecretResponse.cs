using Domain.Entities.IntegrationSecrets;

namespace Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;

/// <summary>Where the value a setting runs on comes from.</summary>
public enum IntegrationSecretSource
{
    /// <summary>Nowhere: the feature runs on built-in defaults, or not at all.</summary>
    None,

    /// <summary>Saved on the Secrets screen. Wins over the server's configuration.</summary>
    Saved,

    /// <summary>The server's own configuration — appsettings.json or an environment variable (.env).</summary>
    Server,
}

/// <param name="Value">
/// Only ever set for a choice — a toggle or a list (<see cref="IsChoice"/>), whose
/// value is a setting rather than information, shown as what is in effect. Every
/// other value stays on the server once saved, secret or not: the screen shows
/// where it comes from, never what it is.
/// </param>
/// <param name="IsSet">A value is saved on the Secrets screen.</param>
/// <param name="HasServerValue">
/// The server's configuration has a value too — the one the setting falls back to
/// if the saved one is removed.
/// </param>
public sealed record IntegrationSecretResponse(
    int Id, string Key, string? Value, bool IsSet, bool HasServerValue, string DisplayName,
    string? Description, IntegrationSecretCategory Category, bool IsSecret,
    bool IsSystem, string? DataType)
{
    public IntegrationSecretSource Source
    {
        get
        {
            if (IsSet)
                return IntegrationSecretSource.Saved;
            return HasServerValue ? IntegrationSecretSource.Server : IntegrationSecretSource.None;
        }
    }

    public bool IsChoice => IsChoiceType(DataType);

    public static bool IsChoiceType(string? dataType) => dataType is "bool" or "storage-provider";
}
