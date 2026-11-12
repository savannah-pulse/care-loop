using System.Text.Json;
using CareLoop.Domain.Cases;

namespace CareLoop.Application.Intake;

public sealed record ActorDto(ActorKind? Kind, string? Id, string? Role);

/// <summary>
/// Shared event envelope (contracts/README.md). Fields are nullable so malformed input is reported
/// as validation errors instead of failing deserialization.
/// </summary>
public sealed record EventEnvelope
{
    public string? EventType { get; init; }
    public string? SchemaVersion { get; init; }
    public string? SourceSystem { get; init; }
    public string? SourceEventId { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>Must be true while the prototype is synthetic-only.</summary>
    public bool? Synthetic { get; init; }

    public Guid? CaseId { get; init; }
    public string? PatientRef { get; init; }
    public string? ResultRef { get; init; }
    public ActorDto? Actor { get; init; }
    public JsonElement? Payload { get; init; }
}
