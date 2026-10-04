using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Concrete;

namespace Infrastructure.Captcha;

/// <summary>
/// Verifies a token against whichever provider is configured in Site Settings.
/// Google reCAPTCHA, hCaptcha, and Cloudflare Turnstile all expose the same
/// request/response shape (form-encoded secret+response → JSON {"success": bool}),
/// so one HTTP call path serves all three.
/// </summary>
internal sealed class CaptchaVerifier(
    HttpClient httpClient,
    IPublicReadDbContext db,
    IOptionsMonitor<CaptchaOptions> options,
    ILogger<CaptchaVerifier> logger) : ICaptchaVerifier
{
    private const double MinimumRecaptchaScore = 0.5;

    /// <summary>
    /// Must match the <c>action</c> the client passes to <c>grecaptcha.execute</c>/
    /// <c>turnstile.execute</c> (see elevare-interactions.js) — the only action this
    /// site currently issues tokens for is a form submission.
    /// </summary>
    private const string ExpectedAction = "submit";

    private static readonly Dictionary<string, string> VerifyUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["recaptcha"] = "https://www.google.com/recaptcha/api/siteverify",
        ["hcaptcha"] = "https://hcaptcha.com/siteverify",
        ["turnstile"] = "https://challenges.cloudflare.com/turnstile/v0/siteverify",
    };

    /// <summary>
    /// Verifies the challenge token against the configured captcha provider.
    /// </summary>
    /// <param name="token">The client-side token submitted with the form.</param>
    /// <param name="cancellationToken">Cancellation token for the HTTP verification call.</param>
    /// <returns>Success if the challenge is passed or disabled; otherwise a detailed error.</returns>
    public Task<Result> VerifyAsync(string? token, CancellationToken cancellationToken = default)
        => VerifyAsync(token, remoteIp: null, cancellationToken);

    /// <summary>
    /// Verifies the challenge token against the configured captcha provider with client IP forwarding.
    /// </summary>
    /// <param name="token">The client-side token submitted with the form.</param>
    /// <param name="remoteIp">Optional client IP address forwarded for risk assessment.</param>
    /// <param name="cancellationToken">Cancellation token for the HTTP verification call.</param>
    /// <returns>Success if the challenge is passed or disabled; otherwise a detailed error.</returns>
    public async Task<Result> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default)
    {
        string? provider = await db.SiteSettings
            .Where(s => s.Key == "Integrations.CaptchaProvider")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        // Captcha switched off in the CMS — nothing to verify, so the submission passes.
        if (string.IsNullOrWhiteSpace(provider) || provider == "none")
            return Result.Success();

        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(CaptchaErrors.Rejected);

        if (!VerifyUrls.TryGetValue(provider, out string? verifyUrl))
        {
            logger.LogWarning(
                "Site Settings names captcha provider '{Provider}', which this build cannot call — rejecting submission.",
                provider);
            return Result.Failure(CaptchaErrors.UnknownProvider(provider));
        }

        string secretKey = options.CurrentValue.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            // Captcha is turned on in the CMS but the deploy-time secret was never
            // set — fail closed rather than silently letting every submission
            // through unprotected.
            logger.LogWarning(
                "Captcha provider '{Provider}' is enabled but no secret key is configured (Captcha:SecretKey) — rejecting submission.",
                provider);
            return Result.Failure(CaptchaErrors.SecretKeyMissing);
        }

        try
        {
            var formValues = new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token,
            };

            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                formValues["remoteip"] = remoteIp;
            }

            using FormUrlEncodedContent content = new(formValues);

            using HttpResponseMessage response = await httpClient.PostAsync(verifyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Captcha provider '{Provider}' returned HTTP {StatusCode} — rejecting submission.",
                    provider, (int)response.StatusCode);
                return Result.Failure(CaptchaErrors.Unavailable($"HTTP {(int)response.StatusCode}"));
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
            CaptchaVerifyResponse? result = await JsonSerializer.DeserializeAsync<CaptchaVerifyResponse>(body, cancellationToken: cancellationToken);

            // A missing/unparseable body is an unavailable provider, not a failed
            // challenge — the visitor did nothing wrong and the distinction is the
            // whole reason this returns a Result.
            if (result is null)
                return Result.Failure(CaptchaErrors.Unavailable("the response body could not be read"));

            if (!result.Success)
            {
                if (result.ErrorCodes is { Count: > 0 })
                {
                    logger.LogWarning(
                        "Captcha provider '{Provider}' rejected challenge. Error codes: {ErrorCodes}",
                        provider, string.Join(", ", result.ErrorCodes));
                }

                return Result.Failure(CaptchaErrors.Rejected);
            }

            // reCAPTCHA v3 score-based check: Google returns success: true even for bots,
            // distinguishing risk levels by score (0.0 = bot, 1.0 = human).
            if (string.Equals(provider, "recaptcha", StringComparison.OrdinalIgnoreCase)
                && result.Score.HasValue
                && result.Score.Value < MinimumRecaptchaScore)
            {
                logger.LogWarning(
                    "reCAPTCHA v3 score {Score} is below threshold {Threshold} — rejecting submission.",
                    result.Score.Value, MinimumRecaptchaScore);
                return Result.Failure(CaptchaErrors.Rejected);
            }

            // Action-based providers (reCAPTCHA v3, Turnstile) echo back the action
            // name the client requested the token for; a mismatch means the token was
            // minted for something else and is being replayed here — reject even
            // though the provider itself reported success. hCaptcha never returns
            // this field, so Action stays null and the check is skipped for it.
            if (result.Action is not null
                && !string.Equals(result.Action, ExpectedAction, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Captcha provider '{Provider}' returned action '{Action}', expected '{Expected}' — rejecting submission.",
                    provider, result.Action, ExpectedAction);
                return Result.Failure(CaptchaErrors.Rejected);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Captcha verification request to '{Provider}' failed.", provider);
            return Result.Failure(CaptchaErrors.Unavailable(ex.Message));
        }
    }

    private sealed record CaptchaVerifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("score")] double? Score = null,
        [property: JsonPropertyName("action")] string? Action = null,
        [property: JsonPropertyName("error-codes")] List<string>? ErrorCodes = null);
}
