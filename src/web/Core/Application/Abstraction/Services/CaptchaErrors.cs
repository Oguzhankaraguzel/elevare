using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Failures from <see cref="ICaptchaVerifier"/>.
/// <para>
/// These exist precisely because a plain <c>bool</c> could not tell them apart. A
/// visitor failing a challenge is normal traffic; a missing secret key or an
/// unreachable provider means every submission on the site is being rejected and
/// somebody needs to know. All of them still reject the submission — the
/// verifier fails closed — but they are no longer the same event.
/// </para>
/// </summary>
public static class CaptchaErrors
{
    /// <summary>The visitor's token was absent or the provider judged it invalid.</summary>
    public static readonly Error Rejected =
        Error.Failure("Captcha.Rejected", "The captcha challenge was not passed.");

    /// <summary>
    /// A provider is enabled in Site Settings but <c>Captcha:SecretKey</c> was never
    /// set at deploy time. Every submission is being refused until it is.
    /// </summary>
    public static readonly Error SecretKeyMissing =
        Error.Problem("Captcha.SecretKeyMissing", "A captcha provider is enabled but no secret key is configured.");

    /// <summary>Site Settings names a provider this build does not know how to call.</summary>
    public static Error UnknownProvider(string provider) =>
        Error.Problem("Captcha.UnknownProvider", $"'{provider}' is not a supported captcha provider.");

    /// <summary>The provider could not be reached, or answered something unusable.</summary>
    public static Error Unavailable(string detail) =>
        Error.Problem("Captcha.Unavailable", $"The captcha provider could not be reached: {detail}");
}
