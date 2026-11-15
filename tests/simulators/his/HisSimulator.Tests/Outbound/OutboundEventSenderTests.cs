using System.Net;
using HisSimulator.Outbound;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;
using Task = System.Threading.Tasks.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class OutboundEventSenderTests
{
    [Fact] public async Task Screening_sends_report_and_only_creates_case_on_acceptance()
    {
        await using var host = await SimulatorHost.StartAsync();
        await host.Sender.SendAsync(SimulatorTestData.Request(), default);
        Assert.IsType<DiagnosticReport>(Assert.Single(host.Handler.Resources));
        Assert.NotNull(host.Store.GetCase("case-1"));
        host.Handler.Reply = _ => new(HttpStatusCode.BadRequest);
        var result = await host.Sender.SendAsync(SimulatorTestData.Request() with { CaseRef = "refused" }, default);
        Assert.False(result.Accepted); Assert.Null(host.Store.GetCase("refused"));
    }
    [Fact] public async Task Initial_consultation_sends_dependency_first_and_later_state_updates_only_task()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        await host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ConsultationUpdate), default);
        Assert.IsType<ServiceRequest>(host.Handler.Resources[0]); Assert.IsType<FhirTask>(host.Handler.Resources[1]);
        await host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ConsultationUpdate, "s2", "accepted"), default);
        Assert.Equal(3, host.Handler.Resources.Count); Assert.Equal("PUT", host.Handler.Requests[2].Method);
        Assert.Equal(host.Handler.Resources[1].Id, host.Handler.Resources[2].Id);
    }
    [Fact] public async Task Partial_referral_refusal_retains_successful_dependency_and_retry_sends_task_only()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        host.Handler.Reply = r => new(r.RequestUri!.AbsolutePath.EndsWith("/Task", StringComparison.Ordinal) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.Created);
        var request = SimulatorTestData.Request(HisEventKind.ReferralUpdate);
        Assert.False((await host.Sender.SendAsync(request, default)).Accepted);
        Assert.Contains("referral-service-request", host.Store.GetCase("case-1")!.ResourceIds.Keys);
        Assert.DoesNotContain("referral-task", host.Store.GetCase("case-1")!.ResourceIds.Keys);
        host.Handler.Reply = null;
        Assert.True((await host.Sender.SendAsync(request, default)).Accepted);
        Assert.Equal(3, host.Handler.Resources.Count); Assert.IsType<FhirTask>(host.Handler.Resources[2]);
    }
    [Fact] public async Task Outcome_is_saved_as_a_typed_evidence_reference_and_closure_has_matching_human_provenance()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        await host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.Outcome, "evidence"), default);
        Assert.StartsWith("Procedure/", host.Store.GetCase("case-1")!.ResourceIds["evidence"], StringComparison.Ordinal);
        await host.Sender.SendAsync(SimulatorTestData.With(SimulatorTestData.Request(HisEventKind.Closure), "evidenceFromStep", "evidence"), default);
        Assert.Equal(2, host.Handler.Resources.Count);
        var closure = Assert.IsType<FhirTask>(host.Handler.Resources[1]); var provenance = Assert.Single(closure.Contained.OfType<Provenance>());
        Assert.Equal("#" + provenance.Id, Assert.Single(closure.RelevantHistory).Reference);
        Assert.Equal("#", Assert.Single(provenance.Target).Reference);
        Assert.Equal("Practitioner/SYN-STAFF-1", Assert.Single(provenance.Agent).Who.Reference);
        Assert.Null(provenance.Meta?.VersionId); Assert.Equal("PUT", host.Handler.Requests[1].Method);
    }
    [Fact] public async Task Unknown_case_and_invalid_dependent_task_send_nothing()
    {
        await using var host = await SimulatorHost.StartAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ClinicalReview), default));
        host.Store.UpsertCase(SimulatorTestData.Case);
        await Assert.ThrowsAsync<ArgumentException>(() => host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ReferralUpdate, status: "invalid"), default));
        Assert.Empty(host.Handler.Requests);
    }
    [Fact] public async Task Concurrent_track_requests_share_one_dependency()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ReferralUpdate, "s" + i), default)));
        Assert.Single(host.Handler.Resources.OfType<ServiceRequest>());
        Assert.Equal(8, host.Handler.Resources.OfType<FhirTask>().Count());
    }
    [Fact] public async Task A_refused_dependency_prevents_sending_the_track_task()
    {
        await using var host = await SimulatorHost.StartAsync(); host.Store.UpsertCase(SimulatorTestData.Case);
        host.Handler.Reply = _ => new(HttpStatusCode.UnprocessableEntity);
        var result = await host.Sender.SendAsync(SimulatorTestData.Request(HisEventKind.ReferralUpdate), default);
        Assert.False(result.Accepted); Assert.Single(result.Messages);
        Assert.IsType<ServiceRequest>(Assert.Single(host.Handler.Resources)); Assert.Empty(host.Store.GetCase("case-1")!.ResourceIds);
    }
}
