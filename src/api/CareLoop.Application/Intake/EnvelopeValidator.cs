using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareLoop.Domain.Cases;

namespace CareLoop.Application.Intake;

public static class EnvelopeSchema
{
    public const string CurrentVersion = "1.0";

    public static readonly IReadOnlySet<string> SupportedVersions = new HashSet<string>(StringComparer.Ordinal)
    {
        CurrentVersion,
    };
}

/// <summary>An envelope that passed structural validation, with a resolved actor and content hash.</summary>
public sealed record ValidatedEnvelope(
    string EventType,
    string SchemaVersion,
    string SourceSystem,
    string SourceEventId,
    DateTimeOffset OccurredAt,
    Guid? CaseId,
    string? PatientRef,
    string? ResultRef,
    EventActor Actor,
    JsonElement? Payload,
    string? PayloadJson,
    string ContentHash);

public static class EnvelopeValidator
{
    private const int MaxIdLength = 200;

    public static (ValidatedEnvelope? Envelope, IReadOnlyDictionary<string, string[]> Errors) Validate(
        EventEnvelope envelope)
    {
        var errors = new Dictionary<string, string[]>();

        var eventType = Required(envelope.EventType, "eventType", errors);
        var sourceSystem = Required(envelope.SourceSystem, "sourceSystem", errors);
        var sourceEventId = Required(envelope.SourceEventId, "sourceEventId", errors);
        var schemaVersion = Required(envelope.SchemaVersion, "schemaVersion", errors);

        if (schemaVersion is not null && !EnvelopeSchema.SupportedVersions.Contains(schemaVersion))
        {
            errors["schemaVersion"] =
                [$"Unsupported version '{schemaVersion}'. Supported: {string.Join(", ", EnvelopeSchema.SupportedVersions)}."];
        }

        if (envelope.OccurredAt is null)
        {
            errors["occurredAt"] = ["occurredAt (source time) is required."];
        }

        if (envelope.Synthetic != true)
        {
            errors["synthetic"] = ["The prototype accepts synthetic data only; synthetic must be true."];
        }

        var actor = ResolveActor(envelope, sourceSystem, errors);

        if (errors.Count > 0 || eventType is null || sourceSystem is null || sourceEventId is null
            || schemaVersion is null || actor is null)
        {
            return (null, errors);
        }

        var payload = envelope.Payload is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } p ? p : (JsonElement?)null;
        var payloadJson = payload is null ? null : JsonCanonicalizer.Canonicalize(payload.Value);
        var occurredAt = envelope.OccurredAt!.Value;

        var hash = ContentHasher.Hash(
            eventType, schemaVersion, sourceSystem, sourceEventId,
            occurredAt.UtcDateTime.ToString("O"), envelope.CaseId?.ToString(), envelope.PatientRef, envelope.ResultRef,
            actor.Kind.ToString(), actor.Id, actor.Role, payloadJson);

        return (new ValidatedEnvelope(
            eventType, schemaVersion, sourceSystem, sourceEventId, occurredAt, envelope.CaseId,
            Blank(envelope.PatientRef), Blank(envelope.ResultRef), actor, payload, payloadJson, hash), errors);
    }

    private static EventActor? ResolveActor(EventEnvelope envelope, string? sourceSystem, Dictionary<string, string[]> errors)
    {
        if (envelope.Actor is null)
        {
            return sourceSystem is null ? null : EventActor.ForSource(sourceSystem);
        }

        var kind = envelope.Actor.Kind;
        if (kind is null || kind == ActorKind.System)
        {
            errors["actor.kind"] = ["actor.kind must be 'sourceSystem' or 'human'."];
            return null;
        }

        var id = Required(envelope.Actor.Id ?? (kind == ActorKind.SourceSystem ? sourceSystem : null), "actor.id", errors);
        var role = Blank(envelope.Actor.Role);
        if (kind == ActorKind.Human && role is null)
        {
            errors["actor.role"] = ["A human actor must declare a role."];
        }

        return id is null ? null : new EventActor(kind.Value, id, role);
    }

    private static string? Required(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
            return null;
        }

        if (value.Length > MaxIdLength)
        {
            errors[field] = [$"{field} must be at most {MaxIdLength} characters."];
            return null;
        }

        return value;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

internal static class JsonCanonicalizer
{
    public static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

public static class ContentHasher
{
    public static string Hash(params string?[] parts)
    {
        var text = string.Join('\u001f', parts.Select(p => p ?? ""));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
