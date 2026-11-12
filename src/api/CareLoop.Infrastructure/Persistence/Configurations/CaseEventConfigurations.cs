using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Infrastructure.Persistence.Configurations;

internal sealed class CaseEventConfiguration : IEntityTypeConfiguration<CaseEvent>
{
    public void Configure(EntityTypeBuilder<CaseEvent> builder)
    {
        builder.ToTable("CaseEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SourceEventId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PatientRef).HasMaxLength(200);
        builder.Property(e => e.ResultRef).HasMaxLength(200);
        builder.Property(e => e.ActorKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ActorRole).HasMaxLength(100);
        builder.Property(e => e.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Disposition).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<CoordinationCase>().WithMany().HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Restrict);

        // Idempotency key: (sourceSystem, sourceEventId), per contracts/README.md.
        builder.HasIndex(e => new { e.SourceSystem, e.SourceEventId }).IsUnique();
        builder.HasIndex(e => new { e.CaseId, e.CaseSequence }).IsUnique();
    }
}

internal sealed class StageTransitionConfiguration : IEntityTypeConfiguration<StageTransition>
{
    public void Configure(EntityTypeBuilder<StageTransition> builder)
    {
        builder.ToTable("StageTransitions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.FromStage).HasConversion<string>().HasMaxLength(40);
        builder.Property(t => t.ToStage).HasConversion<string>().HasMaxLength(40);
        builder.Property(t => t.ActorKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.ActorId).HasMaxLength(200).IsRequired();
        builder.Property(t => t.ActorRole).HasMaxLength(100);
        builder.Property(t => t.Reason).HasMaxLength(1000);

        builder.HasOne<CaseEvent>().WithMany().HasForeignKey(t => t.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.CaseId, t.OccurredAt });
        builder.HasIndex(t => t.EventId);
    }
}

internal sealed class CaseExceptionConfiguration : IEntityTypeConfiguration<CaseException>
{
    public void Configure(EntityTypeBuilder<CaseException> builder)
    {
        builder.ToTable("CaseExceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.OwnerKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.OwnerId).HasMaxLength(200);

        builder.HasIndex(e => new { e.Status, e.RaisedAt });
        builder.HasIndex(e => e.CaseId);
    }
}
