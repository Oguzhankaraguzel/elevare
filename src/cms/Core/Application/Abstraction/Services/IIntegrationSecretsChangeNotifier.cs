using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Applies an <c>IntegrationSecrets</c> write to every process that reads it —
/// called by the update/clear command handlers once the write is committed.
/// Always reloads the CMS's own snapshot; additionally notifies the Web app over
/// HTTP, the same way <c>ICacheClearService</c> reaches across the container
/// boundary, but only when the changed key is one Web actually consumes
/// (<c>Captcha:*</c> — Email/ObjectStorage are CMS-only concerns).
/// </summary>
public interface IIntegrationSecretsChangeNotifier
{
    Task<Result> NotifyAsync(string key, CancellationToken cancellationToken = default);
}
