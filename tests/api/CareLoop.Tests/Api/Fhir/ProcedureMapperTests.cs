using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/Procedure.json.
/// </summary>
public sealed class ProcedureMapperTests
{
    private static readonly ProcedureMapper Mapper = new();

    [Fact]
    public void Completed_procedure_maps_to_an_outcome_with_the_procedure_as_evidence()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<Procedure>("Procedure")));

        Assert.Equal(CaseEventType.OutcomeRecorded, envelope.Type);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal(new DateTimeOffset(2026, 1, 3, 9, 0, 0, TimeSpan.Zero), envelope.OccurredAt);
        Assert.Equal("clinician", envelope.Actor?.Role);
        var payload = Assert.IsType<OutcomePayload>(envelope.Payload);
        Assert.Equal("Procedure/SYN-PROC-0001", payload.EvidenceReference);
        Assert.Equal("diagnosis", payload.EvidenceKind);
    }

    [Theory]
    [InlineData("treatment-plan")]
    [InlineData("treatment-started")]
    [InlineData("surveillance-started")]
    public void Evidence_kind_comes_from_the_procedure_code(string kind)
    {
        var procedure = Sample<Procedure>("Procedure");
        procedure.Code = StatusMap.FromEvidenceKind(kind);

        Assert.Equal(kind, ((OutcomePayload)Map(Mapper, procedure)[0].Payload).EvidenceKind);
    }

    [Fact]
    public void Procedure_without_an_evidence_kind_throws_FormatException()
    {
        var procedure = Sample<Procedure>("Procedure");
        procedure.Code = new CodeableConcept { Text = "PLACEHOLDER" };

        Assert.Throws<FormatException>(() => Map(Mapper, procedure));
    }

    [Fact]
    public void Procedure_that_is_not_completed_maps_to_no_event()
    {
        var procedure = Sample<Procedure>("Procedure");
        procedure.Status = EventStatus.InProgress;

        Assert.Empty(Map(Mapper, procedure));
    }
}
