using SharedKernel.Concrete;

namespace Domain.Entities.IntegrationSecrets;

public static class IntegrationSecretErrors
{
    public static readonly Error NotFound = Error.NotFound("IntegrationSecret.NotFound", "The integration secret was not found.");

    public static Error ReloadFailed(string reason) => Error.Failure(
        "IntegrationSecret.ReloadFailed",
        $"The secret was saved, but the CMS could not apply it live — {reason}. A restart will pick it up.");
}
