using Gateway.AuthService.Services;
using Gateway.Core.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.AuthService.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ApiKeyService _apiKeyService;

    public AuthController(UserService userService, ApiKeyService apiKeyService)
    {
        _userService = userService;
        _apiKeyService = apiKeyService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var (result, error) = await _userService.RegisterAsync(dto, ct);
        if (error is not null)
            return Conflict(new { message = error });

        return Created($"/auth/users/{result!.Id}", result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _userService.LoginAsync(dto, ct);
        if (result is null)
            return Unauthorized(new { message = "Invalid username or password." });

        return Ok(result);
    }

    /// <summary>
    /// Admin-only: issues a new API key for service-to-service auth.
    /// The raw key is returned ONCE and never stored.
    /// </summary>
    [HttpPost("apikeys")]
    public async Task<IActionResult> CreateApiKey([FromBody] CreateApiKeyDto dto, CancellationToken ct)
    {
        // Phase 5 adds JWT admin-role enforcement. Endpoint is open in Phase 3 per spec.
        var result = await _apiKeyService.CreateAsync(dto, ct);
        return Created($"/auth/apikeys/{result.Id}", result);
    }
}
