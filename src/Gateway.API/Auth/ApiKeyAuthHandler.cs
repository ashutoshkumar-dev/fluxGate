using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Gateway.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gateway.API.Auth;

public sealed class ApiKeyAuthOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Authentication handler for the X-Api-Key header.
/// Hashes the raw key with SHA-256 and looks up the hash in the database.
/// </summary>
public sealed class ApiKeyAuthHandler : AuthenticationHandler<ApiKeyAuthOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-Api-Key";

    private readonly IApiKeyRepository _apiKeyRepo;

    public ApiKeyAuthHandler(
        IOptionsMonitor<ApiKeyAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepo)
        : base(options, logger, encoder)
    {
        _apiKeyRepo = apiKeyRepo;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var rawValues) ||
            rawValues.Count == 0 ||
            string.IsNullOrWhiteSpace(rawValues[0]))
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = rawValues[0]!;
        var keyHash = ComputeHash(rawKey);

        var apiKey = await _apiKeyRepo.GetByKeyHashAsync(keyHash);

        if (apiKey is null || !apiKey.IsActive)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("API key has expired.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new(ClaimTypes.Name, apiKey.OwnerService),
            new("service", apiKey.OwnerService)
        };
        foreach (var scope in apiKey.Scopes)
            claims.Add(new Claim("scope", scope));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeHash(string rawKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
