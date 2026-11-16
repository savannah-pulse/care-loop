using System.Net;
using System.Text.Json;
using HisSimulator.State;

namespace HisSimulator.Tests.Inbound;

public sealed class FhirMessageLogEndpointsTests
{
    [Fact] public async Task Log_lists_newest_first_filters_and_preserves_simulated_marker()
    {
        await using var host = await SimulatorHost.StartAsync();
        host.Store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "a", SimulatorTestData.At));
        host.Store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Received, "a", SimulatorTestData.At.AddSeconds(1)));
        host.Store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "b", SimulatorTestData.At.AddSeconds(2)));
        using var all = JsonDocument.Parse(await host.Http.GetStringAsync("/fhir-log"));
        Assert.Equal("b", all.RootElement.GetProperty("entries")[0].GetProperty("caseRef").GetString());
        using var filter = JsonDocument.Parse(await host.Http.GetStringAsync("/fhir-log?direction=received&caseRef=a"));
        var entry = Assert.Single(filter.RootElement.GetProperty("entries").EnumerateArray());
        Assert.True(entry.GetProperty("synthetic").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.GetAsync("/fhir-log?direction=invalid")).StatusCode);
    }
    [Fact] public async Task Log_can_be_limited_resumed_after_an_instant_and_read_by_id()
    {
        await using var host = await SimulatorHost.StartAsync();
        var entries = Enumerable.Range(0, 4).Select(i => SimulatorTestData.Log(FhirMessageDirection.Sent, "c" + i, SimulatorTestData.At.AddSeconds(i))).ToArray();
        foreach (var entry in entries) host.Store.AppendLogEntry(entry);
        using var limited = JsonDocument.Parse(await host.Http.GetStringAsync("/fhir-log?limit=2"));
        Assert.Equal(["c3", "c2"], limited.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("caseRef").GetString()));
        var since = Uri.EscapeDataString(SimulatorTestData.At.AddSeconds(2).ToString("O"));
        using var resumed = JsonDocument.Parse(await host.Http.GetStringAsync("/fhir-log?since=" + since));
        Assert.Equal(["c3"], resumed.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("caseRef").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.GetAsync("/fhir-log?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Http.GetAsync("/fhir-log?since=yesterday")).StatusCode);
        using var one = JsonDocument.Parse(await host.Http.GetStringAsync("/fhir-log/" + entries[1].Id));
        Assert.Equal("c1", one.RootElement.GetProperty("caseRef").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.GetAsync("/fhir-log/" + Guid.NewGuid())).StatusCode);
    }
}
