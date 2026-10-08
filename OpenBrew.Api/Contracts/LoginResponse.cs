namespace OpenBrew.Api.Contracts;

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc);
