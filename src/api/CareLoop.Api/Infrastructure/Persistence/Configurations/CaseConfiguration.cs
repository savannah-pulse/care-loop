using System.Text.Json;
using CareLoop.Domain.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareLoop.Api.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Case"/> aggregate: the case row, its owner, pending handoff, the four tracks,
/// escalations, closure decision and caregiver consent.
/// </summary>
/// <remarks>
/// The Domain has no persistence attributes, so the whole mapping lives here. Value objects are
/// mapped as owned types; enums are stored as strings so the data stays readable and stable if
/// members are reordered.
/// </remarks>
public sealed class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.SourceSystem).HasMaxLength(100).IsRequired();
        builder.Property(c => c.SourceResultId).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PatientReference).HasMaxLength(200).IsRequired();
        builder.Property(c => c.PatientDisplayName).HasMaxLength(200);
        builder.Property(c => c.PatientSecondaryIdentifier).HasMaxLength(200);
        builder.Property(c => c.PathwayId).HasMaxLength(100).IsRequired();
        builder.Property(c => c.FacilityId).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Urgency).HasMaxLength(100);
        builder.Property(c => c.Stage).HasConversion<string>().HasMaxLength(40);
        builder.Property(c => c.NextAction).HasMaxLength(100);
        builder.Property(c => c.Exceptions).HasConversion<int>();
        builder.Property(c => c.OutcomeEvidenceReference).HasMaxLength(200);
        builder.Property(c => c.OutcomeEvidenceKind).HasMaxLength(100);
        builder.Property(c => c.Version).IsConcurrencyToken();

        // One actionable result equals one case.
        builder.HasIndex(c => new { c.SourceSystem, c.SourceResultId }).IsUnique();
        builder.HasIndex(c => c.Stage);
        builder.HasIndex(c => c.DueAt);

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.IsClosed);

        builder.OwnsOne(c => c.Owner, owner =>
        {
            owner.Property(o => o.Reference).HasMaxLength(200).IsRequired();
            owner.Property(o => o.Role).HasMaxLength(100).IsRequired();
            owner.Property(o => o.FacilityId).HasMaxLength(100).IsRequired();
        });
        builder.Navigation(c => c.Owner).IsRequired();

        builder.OwnsOne(c => c.PendingAssignment, assignment =>
        {
            assignment.Property(a => a.Status).HasConversion<string>().HasMaxLength(40);
            assignment.Property(a => a.RejectionReason).HasMaxLength(500);
            assignment.OwnsOne(a => a.ProposedOwner, owner =>
            {
                owner.Property(o => o.Reference).HasMaxLength(200);
                owner.Property(o => o.Role).HasMaxLength(100);
                owner.Property(o => o.FacilityId).HasMaxLength(100);
            });
            assignment.OwnsOne(a => a.ProposedBy, actor =>
            {
                actor.Property(a => a.Reference).HasMaxLength(200);
                actor.Property(a => a.Role).HasMaxLength(100);
                actor.Property(a => a.FacilityId).HasMaxLength(100);
            });
        });

        builder.OwnsMany(c => c.Escalations, escalation =>
        {
            escalation.ToTable("CaseEscalations");
            escalation.WithOwner().HasForeignKey("CaseId");
            // The int "Sequence" key keeps the stored order of escalations; the Domain's Guid Id is what callers name.
            escalation.Property<int>("Sequence");
            escalation.HasKey("Sequence");
            escalation.Property(e => e.Id).ValueGeneratedNever();
            escalation.HasIndex(e => e.Id).IsUnique();
            escalation.Property(e => e.Reason).HasMaxLength(500);
            escalation.Ignore(e => e.IsOpen);
            escalation.Ignore(e => e.IsAcknowledged);
            escalation.OwnsOne(e => e.EscalatedTo, owner =>
            {
                owner.Property(o => o.Reference).HasMaxLength(200);
                owner.Property(o => o.Role).HasMaxLength(100);
                owner.Property(o => o.FacilityId).HasMaxLength(100);
            });
            escalation.OwnsOne(e => e.AcknowledgedBy, actor =>
            {
                actor.Property(a => a.Reference).HasMaxLength(200);
                actor.Property(a => a.Role).HasMaxLength(100);
                actor.Property(a => a.FacilityId).HasMaxLength(100);
            });
        });
        builder.Navigation(c => c.Escalations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsOne(c => c.Notification, track =>
        {
            track.Property(t => t.Status).HasConversion<string>().HasMaxLength(40);
            track.Property(t => t.SourceReference).HasMaxLength(200);
            track.OwnsMany(t => t.Attempts, attempt =>
            {
                attempt.ToTable("NotificationAttempts");
                attempt.WithOwner().HasForeignKey("CaseId");
                attempt.HasKey(a => a.Id);
                attempt.Property(a => a.Id).ValueGeneratedNever();
                attempt.Property(a => a.Channel).HasConversion<string>().HasMaxLength(40);
                attempt.Property(a => a.Status).HasConversion<string>().HasMaxLength(40);
                attempt.Property(a => a.ProviderMessageId).HasMaxLength(200);
                attempt.Property(a => a.FailureReason).HasMaxLength(500);
            });
            track.Navigation(t => t.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
            track.Property(t => t.Confidence);
        });

        builder.OwnsOne(c => c.Consultation, track =>
        {
            track.Property(t => t.Status).HasConversion<string>().HasMaxLength(40);
            track.Property(t => t.SourceReference).HasMaxLength(200);
        });

        builder.OwnsOne(c => c.Referral, track =>
        {
            track.Property(t => t.Status).HasConversion<string>().HasMaxLength(40);
            track.Property(t => t.SourceReference).HasMaxLength(200);
            track.Property(t => t.RejectionReason).HasMaxLength(500);
        });

        builder.OwnsOne(c => c.Appointment, track =>
        {
            track.Property(t => t.Status).HasConversion<string>().HasMaxLength(40);
            track.Property(t => t.SourceReference).HasMaxLength(200);
            track.Property(t => t.OutcomeReference).HasMaxLength(200);
        });

        builder.OwnsOne(c => c.Closure, closure =>
        {
            closure.Property(d => d.Reason).HasConversion<string>().HasMaxLength(40);
            closure.Property(d => d.EvidenceReference).HasMaxLength(200);
            closure.Property(d => d.ExceptionNote).HasMaxLength(2000);
            closure.Property(d => d.DocumentedAttemptReferences)
                .HasConversion(
                    references => JsonSerializer.Serialize(references, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>(),
                    new ValueComparer<IReadOnlyList<string>>(
                        (left, right) => left!.SequenceEqual(right!),
                        references => references.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                        references => references.ToList()));
            closure.OwnsOne(d => d.AuthorizedBy, actor =>
            {
                actor.Property(a => a.Reference).HasMaxLength(200);
                actor.Property(a => a.Role).HasMaxLength(100);
                actor.Property(a => a.FacilityId).HasMaxLength(100);
            });
        });

        // The caregiver's contact point is the one piece of contact data a case holds; it lives and
        // dies with the consent, so it is part of the same owned record.
        builder.OwnsOne(c => c.CaregiverConsent, consent =>
        {
            consent.Ignore(x => x.IsActive);
            consent.Property(x => x.EvidenceReference).HasMaxLength(200).IsRequired();
            consent.Property(x => x.WithdrawalEvidenceReference).HasMaxLength(200);
            consent.OwnsOne(x => x.CaregiverContactPoint, contact =>
            {
                contact.Property(p => p.Channel).HasConversion<string>().HasMaxLength(40).IsRequired();
                contact.Property(p => p.Address).HasMaxLength(500).IsRequired();
            });
            consent.Navigation(x => x.CaregiverContactPoint).IsRequired();
        });
    }
}
