using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Maps Samples/Provenance.json: a closure Provenance containing the completed case Task.
/// </summary>
public sealed class ProvenanceMapperTests
{
    private static readonly ProvenanceMapper Mapper = new();

    [Fact]
    public void Agent_becomes_the_actor_of_the_closure_event()
    {
        var envelope = Assert.Single(Map(Mapper, Sample<Provenance>("Provenance")));

        Assert.Equal(CaseEventType.ClosureDecided, envelope.Type);
        Assert.Equal("Provenance/SYN-PROV-0001/_history/1", envelope.SourceEventId);
        Assert.Equal("SYN-RESULT-0001", envelope.SourceResultId);
        Assert.Equal(new DateTimeOffset(2026, 1, 3, 10, 0, 0, TimeSpan.Zero), envelope.OccurredAt);
        Assert.Equal(new Actor { Reference = "SYN-STAFF-0002", Role = "clinician", FacilityId = "SYN-FAC-A" }, envelope.Actor);

        var payload = Assert.IsType<ClosurePayload>(envelope.Payload);
        Assert.Equal(ClosureReason.Treated, payload.Reason);
        Assert.Equal("Provenance/SYN-PROV-0001", payload.ProvenanceReference);
        Assert.Equal("Procedure/SYN-PROC-0001", payload.EvidenceReference);
    }

    [Fact]
    public void Provenance_without_an_agent_yields_no_authorizer()
    {
        var noAgent = Sample<Provenance>("Provenance");
        noAgent.Agent.Clear();
        var noRole = Sample<Provenance>("Provenance");
        noRole.Agent[0].Who = new ResourceReference("Practitioner/SYN-STAFF-0002");

        Assert.Null(Assert.Single(Map(Mapper, noAgent)).Actor);
        Assert.Null(Assert.Single(Map(Mapper, noRole)).Actor);
    }

    [Fact]
    public void Provenance_for_another_target_maps_to_no_event()
    {
        var procedure = Sample<Provenance>("Provenance");
        procedure.Contained.Clear();
        procedure.Target = [new ResourceReference("Procedure/SYN-PROC-0001")];

        var reviewTask = Sample<Provenance>("Provenance");
        ((FhirTask)reviewTask.Contained[0]).Code = StatusMap.TaskCode(StatusMap.ClinicalReviewTaskCode);

        Assert.Empty(Map(Mapper, procedure));
        Assert.Empty(Map(Mapper, reviewTask));
    }

    [Fact]
    public void Provenance_targeting_a_task_it_does_not_contain_throws_FormatException()
    {
        var provenance = Sample<Provenance>("Provenance");
        provenance.Contained.Clear();
        provenance.Target = [new ResourceReference("Task/SYN-CASE-TASK-0001")];

        Assert.Throws<FormatException>(() => Map(Mapper, provenance));
    }
}
