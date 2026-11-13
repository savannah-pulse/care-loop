namespace HisSimulator.Tests.State;

/// <summary>Planned behaviour of <c>SimulatorStore</c> (staff inbox, message log, cases, runs).</summary>
public sealed class SimulatorStoreTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void AddInboxItem_then_GetInboxItems_returns_it_newest_first()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void AddInboxItem_rejects_a_duplicate_id()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void GetInboxItems_filters_by_recipient()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void GetInboxItem_returns_null_for_an_unknown_id()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void AppendLogEntry_then_GetLogEntries_filters_by_direction_and_case()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void UpsertCase_replaces_an_existing_case()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void SaveRun_then_GetRun_returns_the_latest_state()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Reset_clears_everything()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Store_is_safe_under_concurrent_writes()
    {
    }
}
