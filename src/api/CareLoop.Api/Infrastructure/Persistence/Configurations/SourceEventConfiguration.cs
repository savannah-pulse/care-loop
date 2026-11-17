using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the append-only <see cref="SourceEvent"/> table.
/// </summary>
/// <remarks>
/// The unique index on source system plus source event identifier is what makes ingestion
/// idempotent: a resend of the same state cannot be stored twice even under concurrency.
/// </remarks>
public sealed class SourceEventConfiguration : IEntityTypeConfiguration<SourceEvent>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SourceEvent> builder)
    {
        builder.ToTable("SourceEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.SourceSystem).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SourceEventId).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(60);
        builder.Property(e => e.Disposition).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.DispositionReason).HasMaxLength(1000);
        builder.Property(e => e.EvidenceReference).HasMaxLength(200);
        builder.Property(e => e.Origin).HasMaxLength(20);
        builder.Property(e => e.PayloadJson).IsRequired();

        builder.HasIndex(e => new { e.SourceSystem, e.SourceEventId }).IsUnique();
        builder.HasIndex(e => e.CaseId);

        builder.OwnsOne(e => e.Actor, actor =>
        {
            actor.Property(a => a.Reference).HasMaxLength(200);
            actor.Property(a => a.Role).HasMaxLength(100);
            actor.Property(a => a.FacilityId).HasMaxLength(100);
        });
    }
}
