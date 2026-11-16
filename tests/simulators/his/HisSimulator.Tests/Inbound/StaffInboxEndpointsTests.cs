using System.Net;
using System.Text.Json;

namespace HisSimulator.Tests.Inbound;

public sealed class StaffInboxEndpointsTests
{
    [Fact] public async Task Inbox_is_read_only_filterable_and_detail_is_not_found_for_unknown_id()
    {
        await using var host = await SimulatorHost.StartAsync();
        var old = SimulatorTestData.Inbox("SYN-A", SimulatorTestData.At);
        var newest = SimulatorTestData.Inbox("SYN-B", SimulatorTestData.At.AddSeconds(1));
        host.Store.AddInboxItem(old); host.Store.AddInboxItem(newest);
        using var list = JsonDocument.Parse(await host.Http.GetStringAsync("/staff-inbox"));
        Assert.Equal(newest.Id, list.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var filtered = JsonDocument.Parse(await host.Http.GetStringAsync("/staff-inbox?recipient=SYN-A"));
        Assert.Equal(1, filtered.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await host.Http.GetAsync("/staff-inbox/" + old.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.GetAsync("/staff-inbox/" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Http.PostAsync("/staff-inbox/notifications", new StringContent("{}"))).StatusCode);
        Assert.Empty(host.Handler.Requests);
    }
}
