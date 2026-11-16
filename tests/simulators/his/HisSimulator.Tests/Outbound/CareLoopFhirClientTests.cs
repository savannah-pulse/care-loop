using System.Net;
using HisSimulator.Outbound;
using HisSimulator.State;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Tests.Outbound;

public sealed class CareLoopFhirClientTests
{
    [Fact] public async Task Create_and_update_use_exact_FHIR_paths_media_and_wire_log()
    {
        var handler = new CaptureHandler(); var store = new SimulatorStore();
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, store, new FixedClock());
        var report = new ScreeningResultBuilder().Build(SimulatorTestData.Request());
        await client.CreateAsync(report, "case-1", default); await client.UpdateAsync(report, "case-1", default);
        Assert.Equal("/fhir/DiagnosticReport", handler.Requests[0].Path); Assert.Equal("POST", handler.Requests[0].Method);
        Assert.Equal("/fhir/DiagnosticReport/" + report.Id, handler.Requests[1].Path); Assert.Equal("PUT", handler.Requests[1].Method);
        Assert.All(handler.Requests, r => Assert.Equal("application/fhir+json", r.ContentType));
        Assert.All(store.GetLogEntries(), l => { Assert.Equal(FhirMessageDirection.Sent, l.Direction); Assert.Equal(handler.Requests[0].Body, l.Body); Assert.Equal(l.Body, l.ResponseBody); });
        report.Id = null;
        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateAsync(report, null, default));
    }
    [Theory] [InlineData(200)] [InlineData(404)]
    public async Task GetTask_returns_parsed_task_or_null_only_on_404(int status)
    {
        var handler = new CaptureHandler { Reply = _ => new((HttpStatusCode)status) { Content = new StringContent(SimulatorTestData.Json(SimulatorTestData.CaseTask)) } };
        var store = new SimulatorStore();
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, store, new FixedClock());
        var result = await client.GetTaskAsync("SYN-CASE-1", default);
        if (status == 404) Assert.Null(result); else Assert.Equal("SYN-CASE-1", Assert.IsType<FhirTask>(result).Id);
        Assert.Equal("/fhir/Task/SYN-CASE-1", Assert.Single(handler.Requests).Path);
        Assert.Equal(status, Assert.Single(store.GetLogEntries()).StatusCode);
        Assert.Empty(Assert.Single(store.GetLogEntries()).Body);
        Assert.Contains("\"resourceType\":\"Task\"", Assert.Single(store.GetLogEntries()).ResponseBody);
    }
    [Fact] public async Task Refusal_is_returned_and_its_exact_body_is_logged()
    {
        var handler = new CaptureHandler { Reply = _ => new(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("{\"resourceType\":\"OperationOutcome\"}") } };
        var store = new SimulatorStore();
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, store, new FixedClock());
        var result = await client.CreateAsync(new ScreeningResultBuilder().Build(SimulatorTestData.Request()), null, default);
        Assert.False(result.Accepted); Assert.Equal(422, result.StatusCode);
        Assert.Equal("{\"resourceType\":\"OperationOutcome\"}", Assert.Single(store.GetLogEntries()).ResponseBody);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTaskAsync("SYN-CASE-1", default));
    }
    [Fact] public async Task Transport_and_cancellation_failures_are_logged_and_propagated()
    {
        var handler = new CaptureHandler { Reply = _ => throw new HttpRequestException("Test transport failure") };
        var store = new SimulatorStore();
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, store, new FixedClock());
        await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateAsync(new ScreeningResultBuilder().Build(SimulatorTestData.Request()), null, default));
        Assert.Null(Assert.Single(store.GetLogEntries()).StatusCode);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetTaskAsync("SYN-CASE-1", cts.Token));
        Assert.Equal(2, store.GetLogEntries().Count);
    }
    [Fact] public async Task Non_task_and_malformed_GET_responses_are_refused()
    {
        var handler = new CaptureHandler { Reply = _ => new(HttpStatusCode.OK) { Content = new StringContent("{\"resourceType\":\"Patient\",\"id\":\"SYN-PT-1\"}") } };
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, new SimulatorStore(), new FixedClock());
        await Assert.ThrowsAsync<FormatException>(() => client.GetTaskAsync("SYN-CASE-1", default));
        handler.Reply = _ => new(HttpStatusCode.OK) { Content = new StringContent("{oops") };
        await Assert.ThrowsAsync<FormatException>(() => client.GetTaskAsync("SYN-CASE-1", default));
    }
    [Fact] public async Task A_different_case_task_cannot_be_used_to_correlate_the_requested_case()
    {
        var task = SimulatorTestData.CaseTask; task.Id = "SYN-OTHER-CASE";
        var handler = new CaptureHandler { Reply = _ => new(HttpStatusCode.OK) { Content = new StringContent(SimulatorTestData.Json(task)) } };
        var client = new CareLoopFhirClient(new HttpClient(handler) { BaseAddress = new("http://careloop.invalid/fhir/") }, new SimulatorStore(), new FixedClock());
        await Assert.ThrowsAsync<FormatException>(() => client.GetTaskAsync("SYN-CASE-1", default));
    }
}
