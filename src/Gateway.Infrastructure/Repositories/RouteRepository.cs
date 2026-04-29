using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Repositories;

public class RouteRepository : IRouteRepository
{
    private readonly GatewayDbContext _context;

    public RouteRepository(GatewayDbContext context)
    {
        _context = context;
    }

    public async Task<Route?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Routes.FindAsync([id], ct);
    }

    public async Task<IEnumerable<Route>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.Routes.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        return await query.OrderBy(r => r.CreatedAt).ToListAsync(ct);
    }

    public async Task<Route> AddAsync(Route route, CancellationToken ct = default)
    {
        _context.Routes.Add(route);
        await _context.SaveChangesAsync(ct);
        return route;
    }

    public async Task<Route> UpdateAsync(Route route, CancellationToken ct = default)
    {
        _context.Routes.Update(route);
        await _context.SaveChangesAsync(ct);
        return route;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var route = await _context.Routes.FindAsync([id], ct);
        if (route is null) return false;

        _context.Routes.Remove(route);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
