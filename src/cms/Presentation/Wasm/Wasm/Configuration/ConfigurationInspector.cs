using Application.Abstraction.Services;

namespace Wasm.Configuration;

/// <summary>
/// <see cref="IConfigurationInspector"/> over the app's configuration root. The
/// Secrets layer is <see cref="IntegrationSecretsConfigurationProvider"/>; every
/// other provider is the server's own configuration.
/// </summary>
internal sealed class ConfigurationInspector(IConfiguration configuration) : IConfigurationInspector
{
    public string? GetServerValue(string key)
    {
        if (configuration is not IConfigurationRoot root)
            return NullIfEmpty(configuration[key]);

        // Highest precedence first, and the first provider that has the key decides
        // — even with an empty value. That is how configuration itself resolves it:
        // docker-compose passes SMTP_HOST through as Email__Host="" when unset, and
        // that empty string does override a host in appsettings.json.
        foreach (IConfigurationProvider provider in root.Providers.Reverse())
        {
            if (provider is IntegrationSecretsConfigurationProvider)
                continue;

            if (provider.TryGet(key, out string? value))
                return NullIfEmpty(value);
        }

        return null;
    }

    public string? GetEffectiveValue(string key) => NullIfEmpty(configuration[key]);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
