using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebhookService.Core.Entities;

namespace WebhookService.Infrastructure.Data;

public class WebhookDbContext : DbContext
{
    public WebhookDbContext(DbContextOptions<WebhookDbContext> options) : base(options)
    {
    }

    public DbSet<Subscriber> Subscribers { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Subscriber configuration
        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CallbackUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.EncryptedSecret).IsRequired();
            entity.Property(e => e.KeyId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            // Convert EventTypes list to JSON
            entity.Property(e => e.EventTypes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );

            entity.HasIndex(e => new { e.TenantId, e.IsActive });
            entity.HasIndex(e => e.KeyId).IsUnique();
        });

        // Event configuration
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => new { e.TenantId, e.EventType });
            entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        });

        // Delivery configuration
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.AttemptNumber).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.DurationMs).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasOne(d => d.Event)
                .WithMany(e => e.Deliveries)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Subscriber)
                .WithMany(s => s.Deliveries)
                .HasForeignKey(d => d.SubscriberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.EventId, e.SubscriberId });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
        });
    }
}
