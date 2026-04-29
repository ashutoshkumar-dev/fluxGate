using System.Text.Json;
using Gateway.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gateway.Infrastructure.Persistence.Configurations.Auth;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static readonly ValueComparer<List<string>> ListComparer = new(
        (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
        c => c.ToList());

    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(k => k.KeyHash)
            .HasColumnName("key_hash")
            .IsRequired();

        builder.HasIndex(k => k.KeyHash).IsUnique();

        builder.Property(k => k.OwnerService)
            .HasColumnName("owner_service")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(k => k.Scopes)
            .HasColumnName("scopes")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .Metadata.SetValueComparer(ListComparer);

        builder.Property(k => k.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(k => k.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(k => k.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired(false);
    }
}
