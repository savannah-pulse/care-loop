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
}
