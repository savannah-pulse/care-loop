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
        builder.Property(s => s.Text).IsRequired();
        builder.Property(s => s.DraftedBy).HasMaxLength(100).IsRequired();
        builder.Property(s => s.ApprovedBy).HasMaxLength(200);
        builder.HasIndex(s => s.CaseId);
    }
}
