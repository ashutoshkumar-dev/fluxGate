using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AuthDbContext _context;

    public ApiKeyRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
        => await _context.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

    public async Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(ct);
        return apiKey;
    }
}
