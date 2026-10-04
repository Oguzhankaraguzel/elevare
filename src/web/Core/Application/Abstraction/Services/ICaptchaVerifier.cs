using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Verifies a captcha token from a public form submission against the provider
/// configured in Site Settings (<c>Integrations.CaptchaProvider</c>).
/// <para>
/// Returns success when the challenge passed AND when captcha is switched off
/// entirely — from the caller's point of view both mean "this submission may
/// proceed". Every other outcome is a <see cref="Result"/> failure carrying a
/// <c>CaptchaErrors</c> reason, so an operator can tell a visitor failing a
/// challenge apart from the provider being unreachable or misconfigured.
/// </para>
/// <para>
/// <b>Fails closed:</b> when a provider is enabled but cannot be consulted, the
/// result is a failure — never a pass. An outage at the captcha provider must not
/// silently open the site's forms to spam.
/// </para>
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Verifies the challenge token against the active captcha provider.
    /// </summary>
    /// <param name="token">The client-side captcha challenge token.</param>
    /// <param name="cancellationToken">A cancellation token for the request.</param>
    /// <returns>A successful <see cref="Result"/> if the challenge passed or captcha is disabled; otherwise a failure.</returns>
    Task<Result> VerifyAsync(string? token, CancellationToken cancellationToken = default)
        => VerifyAsync(token, remoteIp: null, cancellationToken);

    /// <summary>
    /// Verifies the challenge token against the active captcha provider with client IP forwarding.
    /// </summary>
    /// <param name="token">The client-side captcha challenge token.</param>
    /// <param name="remoteIp">Optional client IP address to enhance risk assessment.</param>
    /// <param name="cancellationToken">A cancellation token for the request.</param>
    /// <returns>A successful <see cref="Result"/> if the challenge passed or captcha is disabled; otherwise a failure.</returns>
    Task<Result> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default);
}
