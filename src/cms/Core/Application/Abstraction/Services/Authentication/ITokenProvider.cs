namespace Application.Abstraction.Services.Authentication;

public interface ITokenProvider
{
    string GenerateToken(string userId, string userName, string email, string[] roles, string[] permissions, DateTime? expiration = null);
}
