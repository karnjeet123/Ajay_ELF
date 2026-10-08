using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenBrew.Api.Contracts;
using OpenBrew.Api.Options;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IJwtTokenService _tokenService;
    private readonly DemoUserOptions _demoUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IJwtTokenService tokenService,
        IOptions<DemoUserOptions> demoUser,
        ILogger<AuthController> logger)
    {
        _tokenService = tokenService;
        _demoUser = demoUser.Value;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (!string.Equals(request.Username, _demoUser.Username, StringComparison.Ordinal) ||
            !string.Equals(request.Password, _demoUser.Password, StringComparison.Ordinal))
        {
            _logger.LogWarning("Failed login attempt for username {Username}", request.Username);
            return Unauthorized();
        }

        var (accessToken, expiresAtUtc) = _tokenService.GenerateToken(request.Username);

        _logger.LogInformation("Issued JWT token for username {Username}", request.Username);
        return Ok(new LoginResponse(accessToken, expiresAtUtc));
    }
}
