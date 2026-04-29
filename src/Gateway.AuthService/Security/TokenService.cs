using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Gateway.Core.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.AuthService.Security;

public class TokenService
{
    private readonly RsaKeyProvider _keyProvider;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public TokenService(RsaKeyProvider keyProvider, IConfiguration config)
    {
        _keyProvider = keyProvider;
        _issuer = config["Jwt:Issuer"] ?? "fluxgate-auth";
        _audience = config["Jwt:Audience"] ?? "fluxgate-gateway";
        _expiryMinutes = int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var m) ? m : 60;
    }

    public string CreateToken(User user)
    {
        var key = new RsaSecurityKey(_keyProvider.GetSigningKey())
        {
            KeyId = _keyProvider.KeyId
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public object BuildJwks()
    {
        var parameters = _keyProvider.GetPublicParameters();
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = _keyProvider.KeyId,
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                }
            }
        };
    }
}
