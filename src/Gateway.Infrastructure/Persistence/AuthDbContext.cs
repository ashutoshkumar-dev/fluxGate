using Gateway.Core.Domain.Entities;
using Gateway.Infrastructure.Persistence.Configurations.Auth;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Persistence;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
