using HisSimulator.Outbound;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class ClinicalReviewTaskBuilderTests
{
    private readonly ClinicalReviewTaskBuilder _builder = new();
    [Fact] public void Build_returns_completed_task_focused_on_the_screening_report()
    {
        var task = _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal(FhirTask.TaskStatus.Completed, task.Status);
        Assert.Equal("DiagnosticReport/SYN-REPORT-1", task.Focus!.Reference);
    }
    [Fact] public void Build_carries_placeholder_urgency_in_the_agreed_reviewed_extension()
    {
        var task = _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Null(task.Priority);
        Assert.Equal("PLACEHOLDER-URGENCY-A", Assert.IsType<Code>(Assert.Single(task.Extension).Value).Value);
        Assert.Equal(SimulatorTestData.Base + "/StructureDefinition/reviewed-urgency", task.Extension[0].Url);
    }
    [Fact] public void Build_names_the_reviewing_clinician_as_owner() =>
        Assert.Equal("Practitioner/SYN-STAFF-1", _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request()).Owner!.Reference);
    [Fact] public void Build_tags_the_task_as_simulated() => SimulatorTestData.Tagged(_builder.Build(SimulatorTestData.Case, SimulatorTestData.Request()));
    [Fact] public void Build_rejects_a_request_without_performer_or_urgency()
    {
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request() with { Performer = null }));
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Case, SimulatorTestData.Request() with { Params = new Dictionary<string, System.Text.Json.JsonElement>() }));
    }
}
