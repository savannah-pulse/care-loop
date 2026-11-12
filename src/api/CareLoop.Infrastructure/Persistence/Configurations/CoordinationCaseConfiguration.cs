using CareLoop.Domain.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Infrastructure.Persistence.Configurations;

internal sealed class CoordinationCaseConfiguration : IEntityTypeConfiguration<CoordinationCase>
{
    public void Configure(EntityTypeBuilder<CoordinationCase> builder)
    {
        builder.ToTable("Cases");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.SourceSystem).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PatientRef).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ResultRef).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Stage).HasConversion<string>().HasMaxLength(40);
        builder.Property(c => c.OwnerKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.OwnerId).HasMaxLength(200);
        builder.Property(c => c.Version).IsConcurrencyToken();
        builder.Ignore(c => c.Owner);

        builder.HasMany(c => c.Transitions).WithOne().HasForeignKey(t => t.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Exceptions).WithOne().HasForeignKey(e => e.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(c => c.Transitions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One coordination case per source result: the database is the final guard against concurrent intake.
        builder.HasIndex(c => new { c.SourceSystem, c.ResultRef }).IsUnique();
        builder.HasIndex(c => new { c.Stage, c.CreatedAt });
        builder.HasIndex(c => new { c.OwnerKind, c.OwnerId });
    }
}
