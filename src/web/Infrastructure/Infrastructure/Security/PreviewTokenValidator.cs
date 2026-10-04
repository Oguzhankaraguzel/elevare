using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Abstraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

/// <summary>
/// Validates the CMS-signed preview JWT — see <see cref="IPreviewTokenValidator"/>
/// and the CMS's <c>PreviewLinkSigner</c>, which is this token's only producer.
/// No issuer/audience on either side: this is a same-application-family capability
/// token with exactly one verifier, not a token meant to travel between services
/// that need to tell each other apart.
/// </summary>
internal sealed class PreviewTokenValidator(IConfiguration configuration) : IPreviewTokenValidator
{
    // Without this, JwtSecurityTokenHandler silently renames the "sub" claim to
    // the ClaimTypes.NameIdentifier URI on the way out (its default inbound
    // mapping, inherited from WS-Federation) — FindFirstValue(Sub) below would
    // then always return null. Same footgun, same fix, as the CMS's own JwtBearer
    // setup (see its InfrastructureServiceRegistration).
    private static readonly JwtSecurityTokenHandler Handler = new() { MapInboundClaims = false };

    public bool TryValidate(string? token, out int pageId)
    {
        pageId = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string? secret = configuration["Preview:SigningKey"];
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        // CA5404 wants issuer/audience validated — right for a token that crosses
        // trust boundaries, not applicable here: this token has exactly one
        // producer (the CMS) and one consumer (this class), both parts of the same
        // application family sharing one secret. Issuer/audience would be
        // constants asserting a fact the shared secret already guarantees.
#pragma warning disable CA5404
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            // Exact expiry, matching the CMS's stated "~15 minutes" — the default
            // 5-minute grace period would let a link outlive what the editor was
            // told it would.
            ClockSkew = TimeSpan.Zero,
        };
#pragma warning restore CA5404

        ClaimsPrincipal principal;
        try
        {
            principal = Handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            // Malformed input (not three dot-separated segments, bad base64, etc.)
            // — the handler throws for shapes that were never a JWT to begin with,
            // as distinct from one that parsed but failed validation above.
            return false;
        }

        string? sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(sub, out int parsedPageId) || parsedPageId <= 0)
            return false;

        pageId = parsedPageId;
        return true;
    }
}
