using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities.Preview;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Security;

/// <summary>
/// Covers <see cref="PreviewLinkSigner"/> — the CMS half of the preview-link
/// handshake. The public site verifies these tokens with its own
/// <c>PreviewTokenValidator</c> using the same shared secret — but unlike before
/// the JWT switch, both sides now go through <see cref="JwtSecurityTokenHandler"/>
/// rather than a hand-written format, so there is no custom encode/decode logic
/// left for the two sides to drift out of sync on. These tests validate the token
/// with that same standard handler, exactly as the public site does.
/// </summary>
public sealed class PreviewLinkSignerTests
{
    private const string SigningKey = "test-signing-key-0123456789-at-least-32-bytes-long";

    private static PreviewLinkSigner CreateSigner(string? signingKey = SigningKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Preview:SigningKey"] = signingKey })
            .Build();

        return new PreviewLinkSigner(configuration);
    }

    // Mirrors PreviewTokenValidator's own parameters exactly — see the #pragma
    // there for why issuer/audience are off on purpose for this token.
#pragma warning disable CA5404
    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.Zero,
    };
#pragma warning restore CA5404

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reports_a_missing_signing_key_instead_of_throwing(string? signingKey)
    {
        // This used to throw, which turned a missing deploy-time setting into an
        // unhandled 500 on the editor's screen rather than a message naming the key.
        PreviewLinkSigner signer = CreateSigner(signingKey);

        Result<string> result = signer.Sign(42, DateTimeOffset.UtcNow.AddMinutes(30));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(PreviewErrors.SigningKeyMissing);
        result.Error.Description.ShouldContain("Preview:SigningKey");
    }

    [Fact]
    public void Reports_a_signing_key_under_32_bytes_instead_of_letting_the_JWT_library_throw()
    {
        // HMAC-SHA256 requires a 256-bit key; the old hand-rolled HMAC had no such
        // floor and would have silently signed with anything, however weak.
        PreviewLinkSigner signer = CreateSigner("short-key");

        Result<string> result = signer.Sign(42, DateTimeOffset.UtcNow.AddMinutes(30));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(PreviewErrors.SigningKeyTooShort);
    }

    [Fact]
    public void Produces_a_token_the_standard_JWT_handler_accepts_with_the_right_page_id()
    {
        // This is the cross-application contract, proven the same way the public
        // site actually checks it: same library, same validation parameters. If
        // someone changes what PreviewLinkSigner puts in the token, this fails
        // here instead of every preview link quietly 404ing in production.
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddMinutes(15);
        PreviewLinkSigner signer = CreateSigner();

        string token = signer.Sign(42, expiry).Value;

        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        ClaimsPrincipal principal = handler.ValidateToken(token, ValidationParameters(SigningKey), out _);

        principal.FindFirstValue(JwtRegisteredClaimNames.Sub).ShouldBe("42");
    }

    [Fact]
    public void Expiry_is_carried_by_the_token_and_enforced_by_the_handler()
    {
        PreviewLinkSigner signer = CreateSigner();
        string token = signer.Sign(42, DateTimeOffset.UtcNow.AddMinutes(-1)).Value;

        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        Should.Throw<SecurityTokenExpiredException>(
            () => handler.ValidateToken(token, ValidationParameters(SigningKey), out _));
    }

    [Fact]
    public void A_token_signed_with_a_different_key_fails_validation()
    {
        PreviewLinkSigner signer = CreateSigner();
        string token = signer.Sign(42, DateTimeOffset.UtcNow.AddMinutes(15)).Value;

        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        Should.Throw<SecurityTokenSignatureKeyNotFoundException>(
            () => handler.ValidateToken(token, ValidationParameters("a-completely-different-key-also-32-bytes-plus"), out _));
    }

    [Fact]
    public void Different_pages_and_expiries_produce_different_tokens()
    {
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddMinutes(15);
        PreviewLinkSigner signer = CreateSigner();

        string forPage42 = signer.Sign(42, expiry).Value;
        string forPage43 = signer.Sign(43, expiry).Value;
        string laterExpiry = signer.Sign(42, expiry.AddSeconds(60)).Value;

        forPage42.ShouldNotBe(forPage43);
        forPage42.ShouldNotBe(laterExpiry);
    }
}
