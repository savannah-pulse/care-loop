using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the append-only <see cref="AuditEvent"/> table that backs the case timeline.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(60);
        builder.Property(e => e.Disposition).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.ActorReference).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ActorRole).HasMaxLength(100).IsRequired();
        builder.Property(e => e.NewProjectionJson).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.EvidenceReference).HasMaxLength(200);

        builder.HasIndex(e => new { e.CaseId, e.ReceivedTimestamp });
        builder.HasIndex(e => e.SourceEventId);
    }
}
