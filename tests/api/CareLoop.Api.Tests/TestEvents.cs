using System.Net.Http.Json;
using System.Text.Json;

namespace CareLoop.Api.Tests;

/// <summary>Builders for synthetic event envelopes shared by integration tests.</summary>
internal static class TestEvents
{
    public static object ResultReceived(
        string sourceEventId, string resultRef = "RES-001", string? reviewedUrgency = "routine", string patientRef = "SYN-PAT-001") => new
        {
            eventType = "ResultReceived",
            schemaVersion = "1.0",
            sourceSystem = "his-simulator",
            sourceEventId,
            occurredAt = "2026-01-05T08:00:00Z",
            synthetic = true,
            patientRef,
            resultRef,
            payload = reviewedUrgency is null ? (object)new { } : new { reviewedUrgency },
        };

    public static object OwnerAssigned(
        Guid caseId, string sourceEventId, string role = "coordinator", string ownerId = "coordinator-1") => new
        {
            eventType = "OwnerAssigned",
            schemaVersion = "1.0",
            sourceSystem = "careloop-ui",
            sourceEventId,
            occurredAt = "2026-01-05T09:00:00Z",
            synthetic = true,
            caseId,
            actor = new { kind = "human", id = "staff-1", role },
            payload = new { ownerKind = "person", ownerId },
        };

    public static async Task<(HttpResponseMessage Response, JsonElement Body)> PostAsync(HttpClient client, object envelope)
    {
        var response = await client.PostAsJsonAsync("/api/events", envelope);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response, body);
    }

    public static async Task<JsonElement> GetAsync(HttpClient client, string url) =>
        await client.GetFromJsonAsync<JsonElement>(url);
}
