using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class ClinicianActionEndpointsTests
{
    [Theory] [InlineData("clinical-review", "urgency", "PLACEHOLDER-URGENCY-A", "Task")]
    [InlineData("outcome", "evidenceKind", "diagnosis", "Procedure")]
    [InlineData("consultation-request", "status", "requested", "ServiceRequest")]
    [InlineData("consultation-answer", "status", "accepted", "ServiceRequest")]
    [InlineData("referral", "status", "requested", "ServiceRequest")]
    [InlineData("appointment", "status", "booked", "Appointment")]
    [InlineData("consent", "status", "inactive", "Consent")]
    public async Task Manual_actions_use_FHIR_only_and_preserve_source_inputs(string route, string name, string value, string expected)
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var body = new Dictionary<string, object?> { ["caseRef"] = "case-1", ["performer"] = SimulatorTestData.Staff, [name] = value };
        var response = await host.Http.PostAsJsonAsync("/actions/" + route, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(expected, host.Handler.Resources[0].TypeName);
        Assert.All(host.Handler.Requests, r => Assert.StartsWith("/fhir/", r.Path, StringComparison.Ordinal));
        if (route == "clinical-review")
            Assert.Equal(value, Assert.IsType<Code>(Assert.Single(Assert.IsType<FhirTask>(host.Handler.Resources[0]).Extension).Value).Value);
        using var cases = JsonDocument.Parse(await host.Http.GetStringAsync("/cases")); Assert.Equal(1, cases.RootElement.GetProperty("cases").GetArrayLength());
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Referral_decision_and_reason_are_source_authored(bool accepted)
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.PostAsJsonAsync("/actions/referral-decision",
            new { caseRef = "case-1", performer = SimulatorTestData.Staff, accepted, reason = "SIMULATED receiving facility declined" })).StatusCode);
        var task = Assert.IsType<FhirTask>(host.Handler.Resources.Last());
        Assert.Equal(accepted ? FhirTask.TaskStatus.Accepted : FhirTask.TaskStatus.Rejected, task.Status);
        if (!accepted) Assert.Equal("SIMULATED receiving facility declined", task.StatusReason!.Text);
    }
    [Fact] public async Task Closure_uses_one_contained_provenance_carrier_and_reports_refusal()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        host.Handler.Reply = _ => new(HttpStatusCode.UnprocessableEntity);
        var response = await host.Http.PostAsJsonAsync("/actions/closure", new { caseRef = "case-1", performer = SimulatorTestData.Staff, closureReason = "Treated" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.False(json.RootElement.GetProperty("accepted").GetBoolean());
        Assert.Single(Assert.IsType<FhirTask>(Assert.Single(host.Handler.Resources)).Contained.OfType<Provenance>());
    }
    [Fact] public async Task Referral_decision_without_an_accepted_field_is_refused_instead_of_sent_as_a_rejection()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var response = await host.Http.PostAsJsonAsync("/actions/referral-decision",
            new { caseRef = "case-1", performer = SimulatorTestData.Staff, reason = "SIMULATED reason" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(host.Handler.Requests);
    }
    [Fact] public async Task A_completed_referral_is_refused_because_it_is_never_completed_care()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var response = await host.Http.PostAsJsonAsync("/actions/referral", new { caseRef = "case-1", performer = SimulatorTestData.Staff, status = "completed" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(host.Handler.Requests);
    }
    [Theory] [InlineData("Cured")] [InlineData("")]
    public async Task Closure_with_an_unknown_reason_or_a_non_outcome_evidence_key_is_refused_without_sending(string reason)
    {
        await using var host = await SimulatorHost.StartAsync();
        host.Store.UpsertCase(SimulatorTestData.Case with { ResourceIds = new Dictionary<string, string> { ["referral-task"] = "SYN-TASK-9" } });
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PostAsJsonAsync("/actions/closure",
            new { caseRef = "case-1", performer = SimulatorTestData.Staff, closureReason = reason })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.PostAsJsonAsync("/actions/closure",
            new { caseRef = "case-1", performer = SimulatorTestData.Staff, closureReason = "treated", evidenceResourceKey = "referral-task" })).StatusCode);
        Assert.Empty(host.Handler.Requests);
    }
    [Fact] public async Task Closure_accepts_the_kebab_case_reason_and_the_date_only_surveillance_date()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var response = await host.Http.PostAsJsonAsync("/actions/closure", new
        {
            caseRef = "case-1", performer = SimulatorTestData.Staff, closureReason = "on-surveillance", nextSurveillanceDate = "2027-03-04", exceptionNote = "SIMULATED note",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = Assert.IsType<FhirTask>(Assert.Single(host.Handler.Resources));
        Assert.Equal("on-surveillance", task.StatusReason!.Coding[0].Code);
        Assert.Contains(task.Output, o => o.Value is Date { Value: "2027-03-04" });
    }
    [Fact] public async Task Cases_show_patient_mrn_and_the_approved_handoff_summary_once_a_document_was_received()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        using (var before = JsonDocument.Parse(await host.Http.GetStringAsync("/cases")))
        {
            var item = before.RootElement.GetProperty("cases")[0];
            Assert.Equal("SYN-MRN-PT-1", item.GetProperty("patient").GetProperty("mrn").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("approvedHandoffSummary").ValueKind);
        }
        var document = new DocumentReference
        {
            Id = "SYN-DOC-1", Status = DocumentReferenceStatus.Current, Description = "Handoff summary approved by a person",
            Content = [new DocumentReference.ContentComponent { Attachment = new Attachment { ContentType = "text/plain; charset=utf-8", Data = System.Text.Encoding.UTF8.GetBytes("SIMULATED approved text") } }],
            Context = new DocumentReference.ContextComponent { Related = [new ResourceReference("Task/SYN-CASE-1")] },
        };
        await host.Http.PostAsync("/fhir/DocumentReference", new StringContent(SimulatorTestData.Json(document), System.Text.Encoding.UTF8, "application/fhir+json"));
        using var after = JsonDocument.Parse(await host.Http.GetStringAsync("/cases"));
        Assert.Equal("SIMULATED approved text", after.RootElement.GetProperty("cases")[0].GetProperty("approvedHandoffSummary").GetString());
    }
    [Theory] [InlineData("unknown", "SYN-STAFF-1", 404)] [InlineData("case-1", "REAL-STAFF-1", 400)]
    public async Task Invalid_case_or_actor_is_refused_without_sending(string caseRef, string staff, int status)
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var response = await host.Http.PostAsJsonAsync("/actions/clinical-review",
            new { caseRef, performer = new { syntheticId = staff, role = "clinician", facilityId = "SYN-FAC-A" }, urgency = "PLACEHOLDER-URGENCY-A" });
        Assert.Equal(status, (int)response.StatusCode); Assert.Empty(host.Handler.Requests);
    }
}
