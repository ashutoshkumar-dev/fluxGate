using Gateway.Core.Domain.Entities;
using Gateway.Core.DTOs;
using Gateway.Core.Interfaces;

namespace Gateway.Core.Services;

public class RouteService
{
    private readonly IRouteRepository _repository;

    public RouteService(IRouteRepository repository)
    {
        _repository = repository;
    }

    public async Task<RouteDto> CreateAsync(RouteCreateDto dto, CancellationToken ct = default)
    {
        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = dto.Path,
            Method = dto.Method.ToUpperInvariant(),
            Destination = dto.Destination,
            AuthRequired = dto.AuthRequired,
            Roles = dto.Roles,
            RateLimit = dto.RateLimit is null ? null : new RateLimitConfig
            {
                Limit = dto.RateLimit.Limit,
                WindowSeconds = dto.RateLimit.WindowSeconds
            },
            CacheTtlSeconds = dto.CacheTtlSeconds,
            IsActive = dto.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var created = await _repository.AddAsync(route, ct);
        return MapToDto(created);
    }

    public async Task<IEnumerable<RouteDto>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var routes = await _repository.GetAllAsync(isActive, ct);
        return routes.Select(MapToDto);
    }

    public async Task<RouteDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var route = await _repository.GetByIdAsync(id, ct);
        return route is null ? null : MapToDto(route);
    }

    public async Task<RouteDto?> UpdateAsync(Guid id, RouteUpdateDto dto, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing is null) return null;

        existing.Path = dto.Path;
        existing.Method = dto.Method.ToUpperInvariant();
        existing.Destination = dto.Destination;
        existing.AuthRequired = dto.AuthRequired;
        existing.Roles = dto.Roles;
        existing.RateLimit = dto.RateLimit is null ? null : new RateLimitConfig
        {
            Limit = dto.RateLimit.Limit,
            WindowSeconds = dto.RateLimit.WindowSeconds
        };
        existing.CacheTtlSeconds = dto.CacheTtlSeconds;
        existing.IsActive = dto.IsActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await _repository.UpdateAsync(existing, ct);
        return MapToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.DeleteAsync(id, ct);
    }

    private static RouteDto MapToDto(Route route) => new()
    {
        Id = route.Id,
        Path = route.Path,
        Method = route.Method,
        Destination = route.Destination,
        AuthRequired = route.AuthRequired,
        Roles = route.Roles,
        RateLimit = route.RateLimit is null ? null : new RateLimitDto
        {
            Limit = route.RateLimit.Limit,
            WindowSeconds = route.RateLimit.WindowSeconds
        },
        CacheTtlSeconds = route.CacheTtlSeconds,
        IsActive = route.IsActive,
        CreatedAt = route.CreatedAt,
        UpdatedAt = route.UpdatedAt
    };
}
