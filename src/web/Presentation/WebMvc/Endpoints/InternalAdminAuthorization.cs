using System.Security.Cryptography;
using System.Text;

namespace WebMvc.Endpoints;

/// <summary>
/// Shared check for the internal cms→web admin endpoints (<c>CacheEndpoints</c>,
/// <c>SecretsEndpoints</c>) — both are servers calling this app, not browsers, so
/// both authenticate with the same <c>Cache:ClearSecret</c> header rather than a
/// user login. Factored out once two endpoints needed the identical check.
/// </summary>
internal static class InternalAdminAuthorization
{
    internal const string SecretHeaderName = "X-Cache-Secret";

    public static bool IsAuthorized(HttpContext http, string configuredSecret)
    {
        string providedSecret = http.Request.Headers[SecretHeaderName].ToString();

        // An unset secret is never a match — fail closed rather than accepting an
        // empty header against an empty configured value.
        if (string.IsNullOrEmpty(configuredSecret))
            return false;

        // Fixed-time comparison so the response time cannot be used to recover the
        // secret one character at a time. FixedTimeEquals returns false for unequal
        // lengths without branching on their contents.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredSecret),
            Encoding.UTF8.GetBytes(providedSecret));
    }
}
