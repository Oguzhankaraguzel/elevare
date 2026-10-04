using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="IIntegrationSecretsChangeNotifier"/>.</summary>
public static class IntegrationSecretsChangeErrors
{
    public static readonly Error SecretMismatch =
        Error.Failure("IntegrationSecrets.SecretMismatch", "The website rejected the reload request (401). Cache:ClearSecret must be identical, and non-empty, on both the CMS and the website.");

    public static Error BaseUrlNotConfigured(string settingKey) =>
        Error.Failure("IntegrationSecrets.BaseUrlNotConfigured", $"'{settingKey}' is empty. The Web app cannot be notified until the public site address is set.");

    public static Error RequestFailed(int statusCode, string? reason) =>
        Error.Failure("IntegrationSecrets.RequestFailed", $"The website returned {statusCode} ({reason}).");

    public static Error Unreachable(Exception ex) =>
        Error.Failure("IntegrationSecrets.Unreachable", $"The website could not be reached — {ex?.GetType().Name}: {ex?.Message}");
}
