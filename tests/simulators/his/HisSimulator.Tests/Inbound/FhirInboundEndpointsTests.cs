using System.Net;
using HisSimulator.State;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace HisSimulator.Tests.Inbound;

public sealed class FhirInboundEndpointsTests
{
    private static StringContent FhirContent(string body) => new(body, System.Text.Encoding.UTF8, "application/fhir+json");
    internal static FhirTask Notice(string code) => new()
    {
        Id = "SYN-NOTICE-1", Status = FhirTask.TaskStatus.Requested, Intent = FhirTask.TaskIntent.Order,
        Code = new CodeableConcept(SimulatorTestData.Base + "/CodeSystem/task-code", code),
        Owner = new ResourceReference("Practitioner/SYN-STAFF-2"), Focus = new ResourceReference("Task/SYN-CASE-1"),
        Description = "SIMULATED a case needs your action",
    };
    [Theory] [InlineData("escalation", StaffInboxItemKind.Escalation)] [InlineData("call-request", StaffInboxItemKind.CallRequested)]
    [InlineData("staff-notice", StaffInboxItemKind.StaffNotification)]
    public async Task Notice_is_created_correlated_and_replay_updates_one_inbox_item_without_acceptance(string code, StaffInboxItemKind kind)
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case with { CareLoopTaskId = null });
        var body = SimulatorTestData.Json(Notice(code));
        var first = await host.Http.PostAsync("/fhir/Task", FhirContent(body));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.NotNull(first.Headers.Location);
        Assert.IsType<FhirTask>(SimulatorTestData.Parse(await first.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await host.Http.PutAsync("/fhir/Task/SYN-NOTICE-1", FhirContent(body))).StatusCode);
        var item = Assert.Single(host.Store.GetInboxItems()); Assert.Equal(kind, item.Kind); Assert.Equal("SYN-STAFF-2", item.RecipientSyntheticId);
        Assert.Equal("SYN-CASE-1", host.Store.GetCase("case-1")!.CareLoopTaskId);
        Assert.All(host.Handler.Requests, r => Assert.Equal("GET", r.Method));
        Assert.Equal(2, host.Store.GetLogEntries(FhirMessageDirection.Received).Count);
    }
    [Fact] public async Task Early_case_task_delivery_is_retained_and_rejection_updates_handoff_item()
    {
        await using var host = await SimulatorHost.StartAsync(); var task = SimulatorTestData.CaseTask;
        await host.Http.PostAsync("/fhir/Task", FhirContent(SimulatorTestData.Json(task)));
        host.Store.UpsertCase(SimulatorTestData.Case with { CareLoopTaskId = null });
        Assert.Equal("SYN-CASE-1", host.Store.GetCase("case-1")!.CareLoopTaskId);
        Assert.Equal(StaffInboxItemKind.HandoffProposed, Assert.Single(host.Store.GetInboxItems()).Kind);
        task.Status = FhirTask.TaskStatus.Rejected;
        await host.Http.PutAsync("/fhir/Task/" + task.Id, FhirContent(SimulatorTestData.Json(task)));
        Assert.Equal(StaffInboxItemKind.HandoffRejected, Assert.Single(host.Store.GetInboxItems()).Kind);
        task.Status = FhirTask.TaskStatus.Completed;
        await host.Http.PutAsync("/fhir/Task/" + task.Id, FhirContent(SimulatorTestData.Json(task)));
        Assert.Equal(StaffInboxItemKind.StaffNotification, Assert.Single(host.Store.GetInboxItems()).Kind);
        Assert.Empty(host.Handler.Requests);
    }
    [Fact] public async Task Approved_document_reference_is_received_as_handoff_summary()
    {
        await using var host = await SimulatorHost.StartAsync();
        var document = new DocumentReference
        {
            Id = "SYN-DOC-1", Status = DocumentReferenceStatus.Current, Description = "SIMULATED approved text",
            Content = [new DocumentReference.ContentComponent { Attachment = new Attachment { ContentType = "text/plain", Data = System.Text.Encoding.UTF8.GetBytes("SIMULATED approved text") } }],
        };
        var response = await host.Http.PostAsync("/fhir/DocumentReference", FhirContent(SimulatorTestData.Json(document)));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Equal(StaffInboxItemKind.HandoffSummary, Assert.Single(host.Store.GetInboxItems()).Kind);
    }
    [Theory] [InlineData("/fhir/Task", "{broken", 400)]
    [InlineData("/fhir/Task", "{\"resourceType\":\"Patient\"}", 400)]
    [InlineData("/fhir/Patient", "{\"resourceType\":\"Patient\",\"id\":\"SYN-PT-1\"}", 422)]
    [InlineData("/fhir/Task", "{\"resourceType\":\"Task\",\"code\":{\"coding\":[{\"system\":\"https://careloop.invalid/fhir/CodeSystem/task-code\",\"code\":\"staff-notice\"}]} }", 400)]
    [InlineData("/fhir/Task", "{\"resourceType\":\"Task\",\"status\":\"requested\",\"intent\":\"order\",\"code\":{\"coding\":[{\"system\":\"https://careloop.invalid/fhir/CodeSystem/task-code\",\"code\":\"case\"}]},\"basedOn\":[{\"reference\":\"DiagnosticReport/SYN-REPORT-1\"}]}", 400)]
    public async Task Bad_input_is_refused_and_attempt_is_logged(string path, string json, int status)
    {
        await using var host = await SimulatorHost.StartAsync();
        var response = await host.Http.PostAsync(path, FhirContent(json));
        Assert.True((int)response.StatusCode == status, await response.Content.ReadAsStringAsync());
        Assert.Empty(host.Store.GetInboxItems()); Assert.Equal(status, Assert.Single(host.Store.GetLogEntries()).StatusCode);
    }
    [Fact] public async Task Put_requires_matching_url_and_body_id()
    {
        await using var host = await SimulatorHost.StartAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PutAsync("/fhir/Task/different", FhirContent(SimulatorTestData.Json(Notice("escalation"))))).StatusCode);
        Assert.Empty(host.Store.GetInboxItems());
    }
}
