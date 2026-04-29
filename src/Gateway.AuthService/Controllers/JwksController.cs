using Gateway.AuthService.Security;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.AuthService.Controllers;

[ApiController]
public class JwksController : ControllerBase
{
    private readonly TokenService _tokenService;

    public JwksController(TokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet(".well-known/jwks.json")]
    public IActionResult GetJwks()
    {
        return Ok(_tokenService.BuildJwks());
    }
}
