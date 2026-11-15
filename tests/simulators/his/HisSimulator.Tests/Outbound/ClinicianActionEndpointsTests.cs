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
    [Theory] [InlineData("unknown", "SYN-STAFF-1", 404)] [InlineData("case-1", "REAL-STAFF-1", 400)]
    public async Task Invalid_case_or_actor_is_refused_without_sending(string caseRef, string staff, int status)
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        var response = await host.Http.PostAsJsonAsync("/actions/clinical-review",
            new { caseRef, performer = new { syntheticId = staff, role = "clinician", facilityId = "SYN-FAC-A" }, urgency = "PLACEHOLDER-URGENCY-A" });
        Assert.Equal(status, (int)response.StatusCode); Assert.Empty(host.Handler.Requests);
    }
}
