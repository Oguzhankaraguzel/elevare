using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SharedKernel.Extensions.Claims;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal? principal)
    {
        string? userId = principal?.FindFirst(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(userId, out Guid parsedUserId) ?
            parsedUserId :
            throw new ApplicationException("User id is unavailable");
    }
}
