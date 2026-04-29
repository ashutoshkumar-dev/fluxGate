using System.Text.Json;
using Gateway.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.Path)
            .HasColumnName("path")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.Method)
            .HasColumnName("method")
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(r => r.Destination)
            .HasColumnName("destination")
            .IsRequired();

        builder.Property(r => r.AuthRequired)
            .HasColumnName("auth_required")
            .HasDefaultValue(true);

        builder.Property(r => r.Roles)
            .HasColumnName("roles")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>());

        builder.Property(r => r.RateLimit)
            .HasColumnName("rate_limit")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<RateLimitConfig>(v, JsonOptions));

        builder.Property(r => r.CacheTtlSeconds)
            .HasColumnName("cache_ttl_seconds")
            .IsRequired(false);

        builder.Property(r => r.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");
    }
}
