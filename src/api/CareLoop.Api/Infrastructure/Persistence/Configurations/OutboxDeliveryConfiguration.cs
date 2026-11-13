using CareLoop.Api.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="OutboxDelivery"/> table.
/// </summary>
/// <remarks>The unique index guarantees one delivery record per message and subscriber.</remarks>
public sealed class OutboxDeliveryConfiguration : IEntityTypeConfiguration<OutboxDelivery>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OutboxDelivery> builder)
    {
        builder.ToTable("OutboxDeliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Subscriber).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(d => d.LastError).HasMaxLength(2000);

        builder.HasIndex(d => new { d.OutboxMessageId, d.Subscriber }).IsUnique();
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
    }
}
