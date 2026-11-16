using HisSimulator.Outbound;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class ReferralBuilderTests
{
    private readonly ReferralBuilder _builder = new();
    [Fact] public void BuildServiceRequest_returns_referral_request_for_the_synthetic_patient()
    {
        var resource = _builder.BuildServiceRequest(SimulatorTestData.Case,
            SimulatorTestData.With(SimulatorTestData.Request(), "receivingFacility", "SYN-FAC-B"));
        Assert.Equal("Patient/SYN-PT-1", resource.Subject!.Reference); Assert.Equal("referral", resource.Category[0].Coding[0].Code);
        Assert.Equal("Organization/SYN-FAC-B", Assert.Single(resource.Performer).Reference);
    }
    [Theory] [InlineData("requested", FhirTask.TaskStatus.Requested)] [InlineData("accepted", FhirTask.TaskStatus.Accepted)]
    [InlineData("rejected", FhirTask.TaskStatus.Rejected)]
    public void BuildTask_maps_requested_accepted_and_rejected_statuses(string status, FhirTask.TaskStatus expected) =>
        Assert.Equal(expected, _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.With(SimulatorTestData.Request(status: status), "reason", "SIMULATED rejection")).Status);
    [Fact] public void BuildTask_refuses_completed_because_a_referral_is_never_completed_care() =>
        Assert.Throws<ArgumentException>(() => _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request(status: "completed")));
    [Theory] [InlineData("requested")] [InlineData("accepted")] [InlineData("rejected")]
    public void BuildTask_names_the_acting_member_of_staff_as_owner(string status) =>
        Assert.Equal("Practitioner/SYN-STAFF-1", _builder.BuildTask(SimulatorTestData.Case,
            SimulatorTestData.With(SimulatorTestData.Request(status: status), "reason", "SIMULATED")).Owner!.Reference);
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
