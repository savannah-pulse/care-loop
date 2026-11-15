using System.Net;
using System.Text.Json;
using HisSimulator.Outbound;
using HisSimulator.State;
using HisSimulator.Time;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests;

internal static class SimulatorTestData
{
    internal const string Base = "https://careloop.invalid/fhir";
    internal static readonly DateTimeOffset At = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    internal static readonly ActingStaff Staff = new("SYN-STAFF-1", "clinician", "SYN-FAC-A");
    internal static readonly SimulatedPatient Patient = new("SYN-PT-1", "Synthetic Patient 1", "en");
    internal static SimulatedCase Case => new("case-1", Patient, "SYN-FAC-A", "breast-screening", "SYN-REPORT-1",
        "SYN-CASE-1", new Dictionary<string, string>(), null);
    internal static OutboundEventRequest Request(HisEventKind kind = HisEventKind.ScreeningResult, string key = "s1", string? status = "requested") =>
        new(kind, "case-1", key, At, status, Staff, Patient, "breast-screening", Params(new Dictionary<string, object?>
        {
            ["facility"] = "SYN-FAC-A", ["resultCategory"] = "PLACEHOLDER-RESULT-ACTIONABLE",
            ["urgency"] = "PLACEHOLDER-URGENCY-A", ["evidenceKind"] = "diagnosis", ["closureReason"] = "Treated",
        }));
    internal static IReadOnlyDictionary<string, JsonElement> Params(Dictionary<string, object?> values) =>
        values.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    internal static OutboundEventRequest With(OutboundEventRequest request, string name, object? value)
    {
        var parameters = new Dictionary<string, JsonElement>(request.Params) { [name] = JsonSerializer.SerializeToElement(value) };
        return request with { Params = parameters };
    }
    internal static void Tagged(Resource resource) => Assert.Contains(resource.Meta!.Tag,
        tag => tag.System == Base + "/CodeSystem/data-origin" && tag.Code == "simulated");
    internal static string Json(Resource resource) => JsonSerializer.Serialize(resource, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
    internal static Resource Parse(string body) => JsonSerializer.Deserialize<Resource>(body, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector))!;
    internal static FhirTask CaseTask => new()
    {
        Id = Case.CareLoopTaskId, Status = FhirTask.TaskStatus.Requested, Intent = FhirTask.TaskIntent.Order,
        Code = new CodeableConcept(Base + "/CodeSystem/task-code", "case"),
        BasedOn = [new ResourceReference("DiagnosticReport/" + Case.DiagnosticReportId)],
        Owner = new ResourceReference("Practitioner/" + Staff.SyntheticId),
    };
    internal static StaffInboxItem Inbox(string recipient, DateTimeOffset at) =>
        new(Guid.NewGuid(), StaffInboxItemKind.Escalation, at, recipient, "clinician", "SIMULATED action needed", null, null, "Task", null);
    internal static FhirMessageLogEntry Log(FhirMessageDirection direction, string caseRef, DateTimeOffset at) =>
        new(Guid.NewGuid(), direction, at, "POST", "Task", "SYN-TASK-1", 201, caseRef, "{}");
    internal static ScenarioRun Run => new(Guid.NewGuid(), "test", ScenarioRunStatus.Pending, At, null, []);
}

internal sealed class CaptureHandler : HttpMessageHandler
{
    internal List<(string Method, string Path, string Body, string? ContentType)> Requests { get; } = [];
    internal Func<HttpRequestMessage, HttpResponseMessage>? Reply { get; set; }
    internal IReadOnlyList<Resource> Resources => Requests.Where(r => r.Body.Length > 0).Select(r => SimulatorTestData.Parse(r.Body)).ToArray();
    protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        lock (Requests) Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body, request.Content?.Headers.ContentType?.MediaType));
        return Reply?.Invoke(request) ?? new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.Created)
        {
            Content = new StringContent(request.Method == HttpMethod.Get ? SimulatorTestData.Json(SimulatorTestData.CaseTask) : body),
        };
    }
}

internal sealed class FixedClock : ISimulatorClock
{
    public DateTimeOffset UtcNow => SimulatorTestData.At;
}
