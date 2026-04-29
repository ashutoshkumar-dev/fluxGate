using Gateway.Core.Domain.Entities;
using Gateway.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Infrastructure.Persistence;

public class GatewayDbContext : DbContext
{
    public GatewayDbContext(DbContextOptions<GatewayDbContext> options)
        : base(options)
    {
    }

    public DbSet<Route> Routes => Set<Route>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfiguration(new RouteConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
