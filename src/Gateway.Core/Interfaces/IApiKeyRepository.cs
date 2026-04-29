using Gateway.Core.Domain.Entities;

namespace Gateway.Core.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken ct = default);
}
