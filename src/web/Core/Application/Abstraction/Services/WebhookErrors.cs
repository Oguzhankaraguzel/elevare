using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="IErrorWebhookForwarder"/>.</summary>
public static class WebhookErrors
{
    /// <summary>The webhook accepted the connection but answered with an error status.</summary>
    public static Error RejectedByEndpoint(int statusCode) =>
        Error.Problem("Webhook.RejectedByEndpoint", $"The error webhook responded with HTTP {statusCode}.");

    /// <summary>The request never completed — DNS, TLS, timeout, malformed URL.</summary>
    public static Error RequestFailed(string detail) =>
        Error.Problem("Webhook.RequestFailed", $"Posting to the error webhook failed: {detail}");

    /// <summary>The configured URL is not an http(s) address, or targets a private/internal host.</summary>
    public static Error BlockedTarget =>
        Error.Problem(
            "Webhook.BlockedTarget",
            "The error webhook URL must be a public http(s) address — a private or internal target was refused.");
}
