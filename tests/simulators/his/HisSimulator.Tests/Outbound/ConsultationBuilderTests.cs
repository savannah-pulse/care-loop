using HisSimulator.Outbound;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class ConsultationBuilderTests
{
    private readonly ConsultationBuilder _builder = new();
    [Fact] public void BuildServiceRequest_returns_consultation_request_for_the_synthetic_patient()
    {
        var resource = _builder.BuildServiceRequest(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal("Patient/SYN-PT-1", resource.Subject!.Reference); Assert.Equal("consultation", resource.Category[0].Coding[0].Code);
        Assert.Equal("DiagnosticReport/SYN-REPORT-1", Assert.Single(resource.ReasonReference).Reference);
    }
    [Fact] public void BuildTask_requested_names_requester_and_proposed_owner()
    {
        var request = SimulatorTestData.With(SimulatorTestData.Request(), "proposedOwner", new { syntheticId = "SYN-STAFF-2", role = "receiving-clinician", facility = "SYN-FAC-B" });
        var task = _builder.BuildTask(SimulatorTestData.Case, request);
        Assert.Equal("Practitioner/SYN-STAFF-1", task.Requester!.Reference); Assert.Equal("Practitioner/SYN-STAFF-2", task.Owner!.Reference);
    }
    [Theory] [InlineData("accepted", FhirTask.TaskStatus.Accepted)] [InlineData("rejected", FhirTask.TaskStatus.Rejected)] [InlineData("completed", FhirTask.TaskStatus.Completed)]
    public void BuildTask_maps_accepted_rejected_and_completed_statuses(string status, FhirTask.TaskStatus expected) =>
        Assert.Equal(expected, _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.With(SimulatorTestData.Request(status: status), "reason", "SIMULATED rejection")).Status);
    [Fact] public void BuildTask_keeps_one_id_and_changes_version_per_state()
    {
        var one = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request());
        var two = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request(key: "s2", status: "accepted"));
        Assert.Equal(one.Id, two.Id); Assert.NotEqual(one.Meta!.VersionId, two.Meta!.VersionId);
    }
    [Fact] public void BuildTask_rejected_carries_status_reason() =>
        Assert.Equal("SIMULATED rejection", _builder.BuildTask(SimulatorTestData.Case,
            SimulatorTestData.With(SimulatorTestData.Request(status: "rejected"), "reason", "SIMULATED rejection")).StatusReason!.Text);
    [Fact] public void BuildTask_rejects_an_unsupported_status() =>
        Assert.Throws<ArgumentException>(() => _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request(status: "invented")));
    [Fact] public void Built_resources_are_tagged_as_simulated()
    {
        SimulatorTestData.Tagged(_builder.BuildServiceRequest(SimulatorTestData.Case, SimulatorTestData.Request()));
        SimulatorTestData.Tagged(_builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request()));
    }
}
