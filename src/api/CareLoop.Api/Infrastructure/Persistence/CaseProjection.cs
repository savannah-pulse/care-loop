using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Infrastructure.Persistence;

/// <summary>
/// The JSON snapshot of a case written to the audit trail before and after every event.
/// </summary>
/// <remarks>
/// <para>
/// An audit entry must show what changed, so each one carries the case as it was and as it is.
/// The snapshot is a flat, stable shape rather than the serialized aggregate: it names stage,
/// owner, next action, deadline, flags, tracks, escalations and closure, and nothing else. It holds
/// references only, never clinical content or contact details.
/// </para>
/// <para>
/// The shape is shared by <see cref="EfCaseRepository"/> and the in-memory repository used in tests,
/// so the timeline reads the same whichever stored it. Enums are written as strings so the trail
/// stays readable if members are reordered.
/// </para>
/// </remarks>
public sealed record CaseProjection
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>The pathway stage.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The clinician-reviewed urgency, or <see langword="null"/> before review.</summary>
    public string? Urgency { get; init; }

    /// <summary>The accountable owner.</summary>
    public required CaseOwner Owner { get; init; }

    /// <summary>The pending handoff, if any.</summary>
    public Assignment? PendingAssignment { get; init; }

    /// <summary>The single next action.</summary>
    public string? NextAction { get; init; }

    /// <summary>When the next action is due.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>The unresolved exception flags, as names.</summary>
    public required IReadOnlyList<string> Exceptions { get; init; }

    /// <summary>Whether an escalation is open.</summary>
    public required bool IsEscalated { get; init; }

    /// <summary>Every escalation raised, oldest first.</summary>
    public required IReadOnlyList<Escalation> Escalations { get; init; }

    /// <summary>The notification track, when started.</summary>
    public TrackSnapshot? Notification { get; init; }

    /// <summary>The consultation track, when started.</summary>
    public TrackSnapshot? Consultation { get; init; }

    /// <summary>The referral track, when started.</summary>
    public TrackSnapshot? Referral { get; init; }

    /// <summary>The appointment track, when started.</summary>
    public TrackSnapshot? Appointment { get; init; }

    /// <summary>Reference to the recorded outcome evidence.</summary>
    public string? OutcomeEvidenceReference { get; init; }

    /// <summary>The closure decision, once closed.</summary>
    public ClosureDecision? Closure { get; init; }

    /// <summary>The caregiver consent, once one has been recorded, without the caregiver's address.</summary>
    public ConsentSnapshot? CaregiverConsent { get; init; }

    /// <summary>When the case last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>The concurrency version of the projection.</summary>
    public required int Version { get; init; }

    /// <summary>
    /// Takes the snapshot of a case.
    /// </summary>
    /// <param name="case">The case to snapshot.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="case"/> is <see langword="null"/>.</exception>
    public static CaseProjection From(Case @case)
    {
        ArgumentNullException.ThrowIfNull(@case);

        return new CaseProjection
        {
            Stage = @case.Stage,
            Urgency = @case.Urgency,
            Owner = @case.Owner,
            PendingAssignment = @case.PendingAssignment,
            NextAction = @case.NextAction,
            DueAt = @case.DueAt,
            Exceptions = Enum.GetValues<CaseExceptions>()
                .Where(flag => flag != CaseExceptions.None && @case.Exceptions.HasFlag(flag))
                .Select(flag => flag.ToString())
                .ToList(),
            IsEscalated = @case.IsEscalated,
            Escalations = @case.Escalations.ToList(),
            Notification = @case.Notification is { } n
                ? new TrackSnapshot(n.Status.ToString(), n.SourceReference, n.UpdatedAt, n.IsComplete(), n.Attempts.Count, n.CallRequested, n.Confidence)
                : null,
            Consultation = @case.Consultation is { } c
                ? new TrackSnapshot(c.Status.ToString(), c.SourceReference, c.UpdatedAt, c.IsComplete(), null, null, null)
                : null,
            Referral = @case.Referral is { } r
                ? new TrackSnapshot(r.Status.ToString(), r.SourceReference, r.UpdatedAt, r.IsComplete(), null, null, null)
                : null,
            Appointment = @case.Appointment is { } a
                ? new TrackSnapshot(a.Status.ToString(), a.SourceReference, a.UpdatedAt, a.IsComplete(), null, null, null)
                : null,
            OutcomeEvidenceReference = @case.OutcomeEvidenceReference,
            Closure = @case.Closure,
            CaregiverConsent = @case.CaregiverConsent is { } consent
                ? new ConsentSnapshot(consent.CaregiverContactPoint.Channel.ToString(), consent.GrantedAt, consent.WithdrawnAt, consent.IsActive, consent.EvidenceReference, consent.WithdrawalEvidenceReference)
                : null,
            UpdatedAt = @case.UpdatedAt,
            Version = @case.Version,
        };
    }

    /// <summary>
    /// Serializes a case's snapshot for the audit trail.
    /// </summary>
    /// <param name="case">The case to snapshot.</param>
    /// <returns>The snapshot as JSON.</returns>
    public static string ToJson(Case @case) => JsonSerializer.Serialize(From(@case), SerializerOptions);

    /// <summary>
    /// Reads a snapshot back from the audit trail.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="JsonException"><paramref name="json"/> is not a snapshot.</exception>
    public static CaseProjection FromJson(string json) =>
        JsonSerializer.Deserialize<CaseProjection>(json, SerializerOptions)
        ?? throw new JsonException("The audit projection is empty.");

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// The audit-trail view of one track: enough to see where it stands, not its full history.
/// </summary>
/// <param name="Status">The track status, as a name.</param>
/// <param name="SourceReference">Reference of the resource the track follows.</param>
/// <param name="UpdatedAt">When the status last changed.</param>
/// <param name="IsComplete">Whether the track has reached its completing status.</param>
/// <param name="Attempts">How many sends were made; notification track only.</param>
/// <param name="CallRequested">Whether the patient asked to be called; notification track only.</param>
/// <param name="Confidence">The patient's confidence rating, 1 to 5, once given; notification track only.</param>
public sealed record TrackSnapshot(
    string Status,
    string? SourceReference,
    DateTimeOffset UpdatedAt,
    bool IsComplete,
    int? Attempts,
    bool? CallRequested,
    int? Confidence);

/// <summary>
/// The audit-trail view of the caregiver consent: whether a caregiver may be contacted and on what
/// evidence, never the caregiver's address.
/// </summary>
/// <param name="Channel">The channel the caregiver is reached on, as a name.</param>
/// <param name="GrantedAt">When the patient granted the consent.</param>
/// <param name="WithdrawnAt">When it was withdrawn, or <see langword="null"/> while active.</param>
/// <param name="IsActive">Whether a caregiver message may currently be sent.</param>
/// <param name="EvidenceReference">Reference to the record of the consent.</param>
/// <param name="WithdrawalEvidenceReference">Reference to the record of the withdrawal, when there is one.</param>
public sealed record ConsentSnapshot(
    string Channel,
    DateTimeOffset GrantedAt,
    DateTimeOffset? WithdrawnAt,
    bool IsActive,
    string EvidenceReference,
    string? WithdrawalEvidenceReference);
