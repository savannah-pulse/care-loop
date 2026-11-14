using CareLoop.Api.Infrastructure.Persistence.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="CaseReadRecord"/> to the <c>CaseReads</c> table.
/// </summary>
/// <remarks>Indexed by case and by reader, the two questions an access review asks: who looked at this case, and what did this person look at.</remarks>
public sealed class CaseReadRecordConfiguration : IEntityTypeConfiguration<CaseReadRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CaseReadRecord> builder)
    {
        builder.ToTable("CaseReads");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ActorReference).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ActorRole).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ActorFacilityId).HasMaxLength(100);
        builder.Property(r => r.View).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Query).HasMaxLength(500);
        builder.Property(r => r.Reason).HasMaxLength(1000);

        builder.HasIndex(r => new { r.CaseId, r.ReadAt });
        builder.HasIndex(r => new { r.ActorReference, r.ReadAt });
    }
}
