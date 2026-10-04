using Microsoft.Extensions.Configuration;

namespace Wasm.Configuration;

/// <summary>
/// Wraps an already-constructed <see cref="IntegrationSecretsConfigurationProvider"/>
/// so <c>IConfigurationBuilder.Add</c> wires the SAME instance into
/// <c>IConfiguration.Sources</c> that <c>Program.cs</c> also registers in DI as
/// <c>IIntegrationSecretsReloader</c> — one object, two roles, so a reload
/// triggered through DI is visible to the configuration system it belongs to.
/// </summary>
internal sealed class IntegrationSecretsConfigurationSource(IntegrationSecretsConfigurationProvider provider)
    : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => provider;
}
