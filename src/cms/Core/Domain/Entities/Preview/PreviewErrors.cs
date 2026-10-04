using SharedKernel.Concrete;

namespace Domain.Entities.Preview;

public static class PreviewErrors
{
    /// <summary>
    /// <c>Preview:SigningKey</c> is not configured. Names the setting deliberately:
    /// this only ever happens on a fresh/incomplete deployment, and the fix is a
    /// one-line configuration change the operator can make immediately.
    /// </summary>
    public static readonly Error SigningKeyMissing = Error.Problem(
        "Preview.SigningKeyMissing",
        "Preview links cannot be generated because Preview:SigningKey is not configured. It must match the public site's own value.");

    /// <summary>
    /// The JWT library refuses to sign with an HMAC-SHA256 key under 256 bits (32
    /// bytes) — a real cryptographic floor, not an arbitrary one, since a shorter
    /// key is brute-forceable. The previous hand-rolled HMAC implementation had no
    /// such floor and would have silently accepted (and weakly signed with) a
    /// one-character secret. Named explicitly, with the fix, rather than letting
    /// <see cref="System.ArgumentOutOfRangeException"/> escape unhandled — the
    /// whole point of this class returning <see cref="Result{TValue}"/> instead of
    /// throwing.
    /// </summary>
    public static readonly Error SigningKeyTooShort = Error.Problem(
        "Preview.SigningKeyTooShort",
        "Preview:SigningKey must be at least 32 characters — generate one with `openssl rand -base64 48` (or similar) and use the same value on both apps.");
}
