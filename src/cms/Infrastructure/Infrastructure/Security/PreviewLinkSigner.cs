using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Abstraction.Services;
using Domain.Entities.Preview;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Concrete;

namespace Infrastructure.Security;

/// <summary>
/// Signs preview tokens — a standard JWT, HMAC-SHA256, carrying only the page id
/// (as the registered <c>sub</c> claim) and an expiry. The Web app's
/// <c>PreviewTokenValidator</c> verifies it with the same shared
/// <c>Preview:SigningKey</c> secret and the same library
/// (<see cref="JwtSecurityTokenHandler"/>) — replacing an earlier hand-rolled
/// "pageId.expiry.hmacSignature" format that both sides had to re-implement
/// independently and keep byte-for-byte in sync by hand. Deliberately skips
/// issuer/audience: this token has exactly one verifier that is part of the same
/// application family, so there is nothing for those claims to distinguish.
/// </summary>
internal sealed class PreviewLinkSigner(IConfiguration configuration) : IPreviewLinkSigner
{
    public Result<string> Sign(int pageId, DateTimeOffset expiry)
    {
        string? secret = configuration["Preview:SigningKey"];
        if (string.IsNullOrWhiteSpace(secret))
            return Result.Failure<string>(PreviewErrors.SigningKeyMissing);

        // HMAC-SHA256 needs a 256-bit (32-byte) key at minimum; the library throws
        // ArgumentOutOfRangeException below that, which this class's whole contract
        // (Result, never throw) exists to avoid.
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            return Result.Failure<string>(PreviewErrors.SigningKeyTooShort);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims = [new(JwtRegisteredClaimNames.Sub, pageId.ToString(CultureInfo.InvariantCulture))];

        var token = new JwtSecurityToken(claims: claims, expires: expiry.UtcDateTime, signingCredentials: credentials);

        return Result.Success(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
