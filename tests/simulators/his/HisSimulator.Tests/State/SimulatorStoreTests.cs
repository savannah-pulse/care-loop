using HisSimulator.State;

namespace HisSimulator.Tests.State;

public sealed class SimulatorStoreTests
{
    [Fact] public void Inbox_is_ordered_filtered_and_rejects_duplicate_ids()
    {
        var store = new SimulatorStore(); var old = SimulatorTestData.Inbox("SYN-A", SimulatorTestData.At);
        var newest = SimulatorTestData.Inbox("SYN-B", SimulatorTestData.At.AddSeconds(1));
        store.AddInboxItem(old); store.AddInboxItem(newest);
        Assert.Equal(newest, store.GetInboxItems()[0]); Assert.Equal(old, Assert.Single(store.GetInboxItems("SYN-A")));
        Assert.Equal(old, store.GetInboxItem(old.Id)); Assert.Null(store.GetInboxItem(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => store.AddInboxItem(old));
    }
    [Fact] public void Logs_filter_by_both_direction_and_case()
    {
        var store = new SimulatorStore();
        store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "a", SimulatorTestData.At));
        store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Received, "a", SimulatorTestData.At.AddSeconds(1)));
        store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "b", SimulatorTestData.At.AddSeconds(2)));
        Assert.Equal("b", store.GetLogEntries()[0].CaseRef);
        Assert.Single(store.GetLogEntries(FhirMessageDirection.Sent, "a"));
        Assert.Equal(2, store.GetLogEntries(caseRef: "a").Count);
    }
    [Fact] public void Case_upsert_copies_resource_maps_merges_ids_and_preserves_task_correlation()
    {
        var store = new SimulatorStore(); var ids = new Dictionary<string, string> { ["review"] = "r1" };
        store.UpsertCase(SimulatorTestData.Case with { ResourceIds = ids }); ids["review"] = "mutated";
        store.UpsertCase(SimulatorTestData.Case with { CareLoopTaskId = null, ResourceIds = new Dictionary<string, string> { ["outcome"] = "r2" } });
        var stored = Assert.Single(store.GetCases());
        Assert.Equal("r1", stored.ResourceIds["review"]); Assert.Equal("r2", stored.ResourceIds["outcome"]);
        Assert.Equal("SYN-CASE-1", stored.CareLoopTaskId); Assert.Null(store.GetCase("missing"));
    }
    [Fact] public void Log_runs_and_inbox_keep_only_the_newest_entries_up_to_the_capacity()
    {
        var store = new SimulatorStore(3);
        for (var i = 0; i < 5; i++)
        {
            store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "c" + i, SimulatorTestData.At.AddSeconds(i)));
            store.AddInboxItem(SimulatorTestData.Inbox("SYN-" + i, SimulatorTestData.At.AddSeconds(i)));
            store.SaveRun(SimulatorTestData.Run with { Status = ScenarioRunStatus.Completed, StartedAt = SimulatorTestData.At.AddSeconds(i) });
        }
        Assert.Equal(["c4", "c3", "c2"], store.GetLogEntries().Select(e => e.CaseRef));
        Assert.Equal(["SYN-4", "SYN-3", "SYN-2"], store.GetInboxItems().Select(i => i.RecipientSyntheticId));
        var running = SimulatorTestData.Run with { Status = ScenarioRunStatus.Running, StartedAt = SimulatorTestData.At.AddSeconds(-1) };
        store.SaveRun(running); store.SaveRun(SimulatorTestData.Run with { Status = ScenarioRunStatus.Completed });
        Assert.NotNull(store.GetRun(running.RunId));
    }
    [Fact] public void Log_reads_can_be_limited_and_resumed_after_an_instant()
    {
        var store = new SimulatorStore();
        for (var i = 0; i < 5; i++) store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "c" + i, SimulatorTestData.At.AddSeconds(i)));
        Assert.Equal(["c4", "c3"], store.GetLogEntries(limit: 2).Select(e => e.CaseRef));
        Assert.Equal(["c4", "c3"], store.GetLogEntries(since: SimulatorTestData.At.AddSeconds(2)).Select(e => e.CaseRef));
    }
    [Fact] public void Run_replacement_and_reset_clear_all_collections()
    {
        var store = new SimulatorStore(); var run = SimulatorTestData.Run;
        store.SaveRun(run); store.SaveRun(run with { Status = ScenarioRunStatus.Completed });
        Assert.Equal(ScenarioRunStatus.Completed, store.GetRun(run.RunId)!.Status); Assert.Null(store.GetRun(Guid.NewGuid()));
        store.UpsertCase(SimulatorTestData.Case); store.AddInboxItem(SimulatorTestData.Inbox("SYN-A", SimulatorTestData.At));
        store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, "a", SimulatorTestData.At));
        store.Reset();
        Assert.Empty(store.GetCases()); Assert.Empty(store.GetInboxItems()); Assert.Empty(store.GetLogEntries()); Assert.Null(store.GetRun(run.RunId));
    }
    [Fact] public async Task Concurrent_writes_do_not_drop_items()
    {
        var store = new SimulatorStore();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            store.AddInboxItem(SimulatorTestData.Inbox("SYN-" + i, SimulatorTestData.At));
            store.UpsertCase(SimulatorTestData.Case with { CaseRef = i.ToString() });
            store.AppendLogEntry(SimulatorTestData.Log(FhirMessageDirection.Sent, i.ToString(), SimulatorTestData.At));
        })));
        Assert.Equal(100, store.GetCases().Count); Assert.Equal(100, store.GetInboxItems().Count); Assert.Equal(100, store.GetLogEntries().Count);
    }
}
