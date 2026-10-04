using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="IIntegrationSecretsReloader"/>.</summary>
public static class IntegrationSecretsErrors
{
    public static Error ReloadFailed(string reason) => Error.Failure(
        "IntegrationSecrets.ReloadFailed",
        $"Could not reload integration secrets from the database — {reason}.");
}
