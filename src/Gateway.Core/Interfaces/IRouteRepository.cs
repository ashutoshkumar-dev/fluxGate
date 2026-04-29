using Gateway.Core.Domain.Entities;

namespace Gateway.Core.Interfaces;

public interface IRouteRepository
{
    Task<Route?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Route>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task<Route> AddAsync(Route route, CancellationToken ct = default);
    Task<Route> UpdateAsync(Route route, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
