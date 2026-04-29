using System.Security.Cryptography;
using System.Text;
using Gateway.Core.Domain.Entities;
using Gateway.Core.DTOs.Auth;
using Gateway.Core.Interfaces;

namespace Gateway.AuthService.Services;

public class ApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepo;

    public ApiKeyService(IApiKeyRepository apiKeyRepo)
    {
        _apiKeyRepo = apiKeyRepo;
    }

    public async Task<CreateApiKeyResponseDto> CreateAsync(
        CreateApiKeyDto dto, CancellationToken ct = default)
    {
        // Generate a 32-byte cryptographically secure random key, base64url encoded
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Store only the SHA-256 hash
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            KeyHash = keyHash,
            OwnerService = dto.OwnerService,
            Scopes = dto.Scopes,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _apiKeyRepo.AddAsync(apiKey, ct);

        return new CreateApiKeyResponseDto
        {
            Id = apiKey.Id,
            RawKey = rawKey  // returned ONCE, never stored
        };
    }
}
