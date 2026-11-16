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
    [Fact] public void BuildTask_requested_with_a_proposed_owner_carries_that_owner_and_facility()
    {
        var request = SimulatorTestData.With(SimulatorTestData.Request(), "proposedOwner", new { syntheticId = "SYN-STAFF-2", role = "receiving-clinician", facility = "SYN-FAC-B" });
        var owner = _builder.BuildTask(SimulatorTestData.Case, request).Owner!;
        Assert.Equal("Practitioner/SYN-STAFF-2", owner.Reference);
        Assert.Contains(owner.Extension, e => e.Url!.EndsWith("/actor-facility", StringComparison.Ordinal) && ((Hl7.Fhir.Model.FhirString)e.Value!).Value == "SYN-FAC-B");
    }
    [Fact] public void BuildTask_refuses_a_proposed_owner_without_a_facility() =>
        Assert.Throws<ArgumentException>(() => _builder.BuildTask(SimulatorTestData.Case,
            SimulatorTestData.With(SimulatorTestData.Request(), "proposedOwner", new { syntheticId = "SYN-STAFF-2", role = "receiving-clinician" })));
    [Fact] public void BuildTask_requested_without_a_proposed_owner_names_no_owner_so_no_handoff_is_proposed()
    {
        var task = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Null(task.Owner); Assert.Equal("Practitioner/SYN-STAFF-1", task.Requester!.Reference);
    }
    [Theory] [InlineData("accepted")] [InlineData("rejected")] [InlineData("completed")]
    public void BuildTask_answers_name_the_answering_member_of_staff_as_owner(string status) =>
        Assert.Equal("Practitioner/SYN-STAFF-1", _builder.BuildTask(SimulatorTestData.Case,
            SimulatorTestData.With(SimulatorTestData.Request(status: status), "reason", "SIMULATED")).Owner!.Reference);
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
