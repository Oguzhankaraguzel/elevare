using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace ArchitectureTests.Security;

/// <summary>
/// Covers <see cref="PreviewTokenValidator"/> — the public site's half of the
/// preview-link handshake. Tokens here are built with the same
/// <see cref="JwtSecurityTokenHandler"/> the CMS's <c>PreviewLinkSigner</c> uses in
/// production, not a hand-rolled stand-in — so unlike the old format, a change to
/// how one side encodes a token cannot drift silently out of sync with how the
/// other side decodes it: both call into the same library.
/// </summary>
public sealed class PreviewTokenValidatorTests
{
    private const string Secret = "test-signing-key-0123456789-at-least-32-bytes-long";

    private static PreviewTokenValidator CreateValidator(string? signingKey = Secret)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Preview:SigningKey"] = signingKey })
            .Build();

        return new PreviewTokenValidator(configuration);
    }

    private static string MakeToken(int pageId, DateTimeOffset expiry, string secret = Secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        Claim[] claims = [new(JwtRegisteredClaimNames.Sub, pageId.ToString(CultureInfo.InvariantCulture))];
        var token = new JwtSecurityToken(claims: claims, expires: expiry.UtcDateTime, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void Valid_unexpired_token_is_accepted_with_correct_page_id()
    {
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(15));

        bool ok = CreateValidator().TryValidate(token, out int pageId);

        ok.ShouldBeTrue();
        pageId.ShouldBe(42);
    }

    [Fact]
    public void Expired_token_is_rejected()
    {
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(-1));

        bool ok = CreateValidator().TryValidate(token, out int pageId);

        ok.ShouldBeFalse();
        pageId.ShouldBe(0);
    }

    [Fact]
    public void Tampered_signature_is_rejected()
    {
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(15));
        int sigStart = token.LastIndexOf('.') + 1;
        char original = token[sigStart];
        string tampered = token[..sigStart] + (original == 'A' ? 'B' : 'A') + token[(sigStart + 1)..];

        CreateValidator().TryValidate(tampered, out int pageId).ShouldBeFalse();
        pageId.ShouldBe(0);
    }

    [Fact]
    public void Token_signed_with_a_different_key_is_rejected()
    {
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(15), secret: "a-completely-different-key-also-32-bytes-plus");

        CreateValidator().TryValidate(token, out int pageId).ShouldBeFalse();
        pageId.ShouldBe(0);
    }

    [Fact]
    public void Missing_signing_key_is_rejected_rather_than_throwing()
    {
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(15));

        CreateValidator(signingKey: null).TryValidate(token, out int pageId).ShouldBeFalse();
        pageId.ShouldBe(0);
    }

    [Fact]
    public void A_signing_key_under_32_bytes_is_rejected_rather_than_throwing()
    {
        // The JWT library throws ArgumentOutOfRangeException for an HMAC-SHA256 key
        // under 256 bits — TryValidate's contract is bool, never an exception, so
        // this has to come back as a plain rejection like any other bad input.
        string token = MakeToken(42, DateTimeOffset.UtcNow.AddMinutes(15));

        CreateValidator(signingKey: "short-key").TryValidate(token, out int pageId).ShouldBeFalse();
        pageId.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("only-one-part")]
    [InlineData("two.parts")]
    [InlineData("not-even-close-to-a-jwt")]
    public void Malformed_input_is_rejected(string? token)
    {
        CreateValidator().TryValidate(token, out int pageId).ShouldBeFalse();
        pageId.ShouldBe(0);
    }
}
