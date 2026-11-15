using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HisSimulator.Tests.Scenarios;

public sealed class ScenarioEndpointsTests
{
    [Fact] public async Task List_start_and_progress_have_expected_HTTP_contracts()
    {
        using var folder = new ScenarioFolder(); await using var host = await SimulatorHost.StartAsync(folder.Path);
        using var list = JsonDocument.Parse(await host.Http.GetStringAsync("/scenarios"));
        var summary = Assert.Single(list.RootElement.GetProperty("scenarios").EnumerateArray());
        Assert.Equal(2, summary.GetProperty("hisStepCount").GetInt32());
        var response = await host.Http.PostAsJsonAsync("/scenarios/test/run", new { realSecondsPerScenarioHour = 0 });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); Assert.NotNull(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.GetAsync(response.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.GetAsync("/scenarios/runs/" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.PostAsJsonAsync("/scenarios/missing/run", new { realSecondsPerScenarioHour = 0 })).StatusCode);
    }
}
