using HisSimulator.Outbound;
using Hl7.Fhir.Model;

namespace HisSimulator.Tests.Outbound;

public sealed class ScreeningResultBuilderTests
{
    private readonly ScreeningResultBuilder _builder = new();
    [Fact] public void Build_returns_final_diagnostic_report_for_the_synthetic_patient()
    {
        var report = _builder.Build(SimulatorTestData.Request());
        Assert.Equal(DiagnosticReport.DiagnosticReportStatus.Final, report.Status);
        var patient = Assert.Single(report.Contained.OfType<Patient>());
        Assert.Equal("#" + patient.Id, report.Subject!.Reference);
        Assert.Equal("Synthetic Patient 1", Assert.Single(patient.Name).Text);
        Assert.StartsWith("SYN-MRN-", Assert.Single(patient.Identifier).Value, StringComparison.Ordinal);
        Assert.Equal(ContactPoint.ContactPointSystem.Other, Assert.Single(patient.Telecom).System);
    }
    [Fact] public void Build_contains_observations_referenced_from_result()
    {
        var report = _builder.Build(SimulatorTestData.Request());
        Assert.All(report.Result, r => Assert.Contains(report.Contained.OfType<Observation>(), o => "#" + o.Id == r.Reference));
        Assert.NotEmpty(report.Result);
    }
    [Fact] public void Build_passes_placeholder_result_category_through_unchanged() =>
        Assert.Equal("PLACEHOLDER-RESULT-ACTIONABLE", Assert.Single(_builder.Build(SimulatorTestData.Request()).ConclusionCode).Coding[0].Code);
    [Fact] public void Build_derives_the_same_id_from_the_same_source_event_key()
    {
        var one = _builder.Build(SimulatorTestData.Request());
        Assert.Equal(one.Id, _builder.Build(SimulatorTestData.Request()).Id);
        Assert.NotEqual(one.Id, _builder.Build(SimulatorTestData.Request(key: "different")).Id);
    }
    [Fact] public void Build_tags_the_report_as_simulated() => SimulatorTestData.Tagged(_builder.Build(SimulatorTestData.Request()));
    [Fact] public void Build_rejects_a_request_without_patient_or_result_category()
    {
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Request() with { Patient = null }));
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Request() with { Params = new Dictionary<string, System.Text.Json.JsonElement>() }));
        Assert.Throws<ArgumentException>(() => _builder.Build(SimulatorTestData.Request() with { Patient = new("REAL-1", "A real name", "en") }));
    }
    [Fact] public void CreateCase_records_patient_facility_pathway_and_report_id()
    {
        var request = SimulatorTestData.Request();
        var report = _builder.Build(request);
        var @case = _builder.CreateCase(request, report);
        Assert.Equal(request.Patient, @case.Patient); Assert.Equal("SYN-FAC-A", @case.FacilityId);
        Assert.Equal(request.PathwayId, @case.PathwayId); Assert.Equal(report.Id, @case.DiagnosticReportId);
    }
}
