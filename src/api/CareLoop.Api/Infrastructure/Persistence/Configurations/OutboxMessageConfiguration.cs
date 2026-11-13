using CareLoop.Api.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="OutboxMessage"/> table and its deliveries.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
        builder.HasIndex(m => m.CaseId);

        builder.HasMany(m => m.Deliveries)
            .WithOne()
            .HasForeignKey(d => d.OutboxMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
