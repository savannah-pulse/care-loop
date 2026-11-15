using HisSimulator.Outbound;
using Hl7.Fhir.Model;

namespace HisSimulator.Tests.Outbound;

public sealed class OutcomeBuilderTests
{
    private readonly OutcomeBuilder _builder = new();
    [Fact] public void BuildProcedure_returns_completed_procedure_with_placeholder_evidence_kind()
    {
        var resource = _builder.BuildProcedure(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal(EventStatus.Completed, resource.Status); Assert.Equal("diagnosis", resource.Code!.Coding[0].Code);
    }
    [Fact] public void BuildFollowUpReport_references_the_original_screening_report() =>
        Assert.Equal("DiagnosticReport/SYN-REPORT-1",
            Assert.IsType<ResourceReference>(Assert.Single(_builder.BuildFollowUpReport(SimulatorTestData.Case, SimulatorTestData.Request()).Extension).Value).Reference);
    [Fact] public void BuildFollowUpReport_is_distinguishable_from_a_new_screening_result()
    {
        var report = _builder.BuildFollowUpReport(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal(SimulatorTestData.Base + "/CodeSystem/evidence-kind", report.Code!.Coding[0].System);
        Assert.Contains(report.Extension, e => e.Url == SimulatorTestData.Base + "/StructureDefinition/screening-result");
    }
    [Fact] public void Built_outcome_has_deterministic_id_and_simulated_tag()
    {
        var one = _builder.BuildProcedure(SimulatorTestData.Case, SimulatorTestData.Request());
        Assert.Equal(one.Id, _builder.BuildProcedure(SimulatorTestData.Case, SimulatorTestData.Request()).Id);
        SimulatorTestData.Tagged(one); SimulatorTestData.Tagged(_builder.BuildFollowUpReport(SimulatorTestData.Case, SimulatorTestData.Request()));
    }
    [Fact] public void Build_rejects_a_request_without_evidence_kind()
    {
        var request = SimulatorTestData.Request() with { Params = new Dictionary<string, System.Text.Json.JsonElement>() };
        Assert.Throws<ArgumentException>(() => _builder.BuildProcedure(SimulatorTestData.Case, request));
        Assert.Throws<ArgumentException>(() => _builder.BuildFollowUpReport(SimulatorTestData.Case, request));
    }
}
