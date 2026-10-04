using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Forwards an error entry to the external endpoint configured in Site Settings
/// (<c>Integrations.ErrorWebhookUrl</c>), so an APM/error-tracking tool receives
/// the same errors <c>AppLogs</c> already stores.
/// <para>
/// Returns success when the post was accepted AND when no webhook is configured
/// (nothing to do is not a failure). A failure result means the forward did not
/// happen — the caller decides what that is worth, but must not let it break the
/// logging it was called from: the local <c>AppLogs</c> write is the record of
/// truth, and the webhook is a copy.
/// </para>
/// </summary>
public interface IErrorWebhookForwarder
{
    Task<Result> ForwardAsync(string message, string? exception, string? path, CancellationToken cancellationToken = default);
}
