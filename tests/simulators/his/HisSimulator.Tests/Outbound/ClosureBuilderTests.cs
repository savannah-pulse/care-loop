using HisSimulator.Outbound;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class ClosureBuilderTests
{
    private readonly ClosureBuilder _builder = new();
    [Fact] public void BuildTask_returns_completed_case_task_with_typed_closure_reason()
    {
        var task = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal(FhirTask.TaskStatus.Completed, task.Status); Assert.Equal("Treated", task.StatusReason!.Coding[0].Code);
    }
    [Fact] public void BuildTask_cites_the_outcome_resource_named_by_evidenceFromStep()
    {
        var @case = SimulatorTestData.Case with { ResourceIds = new Dictionary<string, string> { ["s7"] = "Procedure/SYN-OUTCOME-1" } };
        var task = _builder.BuildTask(@case, SimulatorTestData.With(SimulatorTestData.Request(), "evidenceFromStep", "s7"));
        Assert.Equal("Procedure/SYN-OUTCOME-1", Assert.IsType<ResourceReference>(Assert.Single(task.Output).Value).Reference);
    }
    [Fact] public void BuildTask_sends_no_evidence_when_none_was_supplied() => Assert.Empty(_builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request()).Output);
    [Fact] public void BuildTask_fails_when_the_careloop_task_id_is_unknown() =>
        Assert.Throws<InvalidOperationException>(() => _builder.BuildTask(SimulatorTestData.Case with { CareLoopTaskId = null }, SimulatorTestData.Request()));
    [Fact] public void BuildProvenance_targets_the_case_task_and_names_the_authorizer()
    {
        var task = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request());
        var provenance = _builder.BuildProvenance(SimulatorTestData.Case, SimulatorTestData.Request(), task);
        Assert.Equal("Task/" + task.Id, Assert.Single(provenance.Target).Reference);
        Assert.Equal("Practitioner/SYN-STAFF-1", Assert.Single(provenance.Agent).Who!.Reference);
        Assert.Equal(SimulatorTestData.At, provenance.Recorded);
    }
    [Fact] public void BuildProvenance_rejects_a_request_without_performer() =>
        Assert.Throws<ArgumentException>(() => _builder.BuildProvenance(SimulatorTestData.Case,
            SimulatorTestData.Request() with { Performer = null }, _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request())));
    [Fact] public void Built_resources_are_tagged_as_simulated()
    {
        var task = _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.Request());
        SimulatorTestData.Tagged(task); SimulatorTestData.Tagged(_builder.BuildProvenance(SimulatorTestData.Case, SimulatorTestData.Request(), task));
    }
    [Fact] public void Missing_explicit_evidence_is_refused_instead_of_silently_omitted() =>
        Assert.Throws<ArgumentException>(() => _builder.BuildTask(SimulatorTestData.Case, SimulatorTestData.With(SimulatorTestData.Request(), "evidenceFromStep", "missing")));
}
