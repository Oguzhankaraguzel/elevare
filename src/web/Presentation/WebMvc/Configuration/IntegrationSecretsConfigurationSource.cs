using Microsoft.Extensions.Configuration;

namespace WebMvc.Configuration;

/// <summary>
/// Wraps an already-constructed <see cref="IntegrationSecretsConfigurationProvider"/>
/// so it can serve both as an <see cref="IConfigurationProvider"/> and as the DI
/// singleton <c>IIntegrationSecretsReloader</c> — see the CMS's copy of this file
/// for the full rationale, identical here.
/// </summary>
internal sealed class IntegrationSecretsConfigurationSource(IntegrationSecretsConfigurationProvider provider)
    : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => provider;
}
