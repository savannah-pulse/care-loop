using CareLoop.Api.Infrastructure.Persistence.Summaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="HandoffSummaryRecord"/> table.
/// </summary>
public sealed class HandoffSummaryRecordConfiguration : IEntityTypeConfiguration<HandoffSummaryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<HandoffSummaryRecord> builder)
    {
        builder.ToTable("HandoffSummaries");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(s => s.SectionsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Text).IsRequired();
        builder.Property(s => s.MissingOrConflictingFactsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.SourceFactReferencesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.DraftedBy).HasMaxLength(100).IsRequired();
        builder.Property(s => s.ModelDeployment).HasMaxLength(200).IsRequired();
        builder.Property(s => s.PromptVersion).HasMaxLength(60).IsRequired();
        builder.Property(s => s.PolicyVersion).HasMaxLength(60).IsRequired();
        builder.Property(s => s.Trigger).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Language).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ApprovedBy).HasMaxLength(200);
        builder.Property(s => s.RejectedBy).HasMaxLength(200);
        builder.Property(s => s.RejectionReason).HasMaxLength(1000);
        builder.HasIndex(s => s.CaseId);

        // One unapproved draft per case at a time: the handoff trigger and the case screen must not race into two.
        builder.HasIndex(s => new { s.CaseId, s.Status });
    }
}
