using Gateway.Core.Domain.Entities;

namespace Gateway.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string username, string email, CancellationToken ct = default);
    Task<User> AddAsync(User user, CancellationToken ct = default);
}
