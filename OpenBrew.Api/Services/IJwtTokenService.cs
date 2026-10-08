namespace OpenBrew.Api.Services;

public interface IJwtTokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) GenerateToken(string username);
}
