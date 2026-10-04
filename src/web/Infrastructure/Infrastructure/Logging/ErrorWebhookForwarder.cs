using System.Text.Json;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Infrastructure.Logging;

/// <summary>
/// Posts a small JSON payload to <c>Integrations.ErrorWebhookUrl</c> (Site
/// Settings) when configured — lets an external error-tracking/APM tool receive
/// the same errors <c>AppLogs</c> already stores. Reports failures as a
/// <c>Result</c> instead of throwing, so a down/misconfigured webhook can never
/// break the error logging it was called from.
/// </summary>
internal sealed class ErrorWebhookForwarder(HttpClient httpClient, IPublicReadDbContext db, ILogger<ErrorWebhookForwarder> logger)
    : IErrorWebhookForwarder
{
    public async Task<Result> ForwardAsync(string message, string? exception, string? path, CancellationToken cancellationToken = default)
    {
        try
        {
            string? webhookUrl = await db.SiteSettings
                .Where(s => s.Key == "Integrations.ErrorWebhookUrl")
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);

            // No webhook configured is not a failure — there is simply nothing to do.
            if (string.IsNullOrWhiteSpace(webhookUrl))
                return Result.Success();

            // The URL is operator-set, but this call fires automatically on every
            // server error — so a URL pointed at an internal address would turn the
            // error path into an SSRF probe of the host's own network. Refuse anything
            // that is not a public http(s) endpoint before making the request.
            if (!OutboundUrlPolicy.IsPublicHttpUrl(webhookUrl))
            {
                logger.LogWarning("Refused to forward an error entry to a non-public webhook URL.");
                return Result.Failure(WebhookErrors.BlockedTarget);
            }

            using StringContent content = new(
                JsonSerializer.Serialize(new { message, exception, path, timestampUtc = DateTime.UtcNow }),
                System.Text.Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await httpClient.PostAsync(webhookUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Error webhook returned {StatusCode}.", (int)response.StatusCode);
                return Result.Failure(WebhookErrors.RejectedByEndpoint((int)response.StatusCode));
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or UriFormatException)
        {
            logger.LogWarning(ex, "Failed to forward an error entry to the configured webhook.");
            return Result.Failure(WebhookErrors.RequestFailed(ex.Message));
        }
    }
}
